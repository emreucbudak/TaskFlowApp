using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Infrastructure.Helpers;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class TasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    IdentityApiClient identityApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int TasksPageSize = 100;
    private const int PreviewTaskCount = 4;

    private readonly List<CompanyTaskDto> allIndividualTasks = [];
    private readonly List<CompanyTaskDto> allGroupTasks = [];
    private bool isShowingAllIndividualTasks;
    private bool isShowingAllGroupTasks;

    [ObservableProperty]
    private bool canShowAllIndividualTasks;

    [ObservableProperty]
    private bool canShowAllGroupTasks;

    public ObservableCollection<CompanyTaskDto> IndividualTasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> GroupTasks { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is null)
        {
            ErrorMessage = "Kullanici bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await LoadWorkerReportAccessStateAsync();

            var userId = UserSession.UserId.Value;
            var individualTasksTask = LoadAllIndividualTasksAsync(userId);
            var groupTasksTask = LoadAllGroupTasksSafeAsync(userId, UserSession.CompanyId);

            await Task.WhenAll(individualTasksTask, groupTasksTask);

            var individualTasks = TaskHelper.OrderTasks(await individualTasksTask);
            var groupTasks = TaskHelper.OrderTasks(await groupTasksTask);

            allIndividualTasks.Clear();
            allGroupTasks.Clear();
            allIndividualTasks.AddRange(individualTasks);
            allGroupTasks.AddRange(groupTasks);

            isShowingAllIndividualTasks = false;
            isShowingAllGroupTasks = false;
            CanShowAllIndividualTasks = allIndividualTasks.Count > PreviewTaskCount;
            CanShowAllGroupTasks = allGroupTasks.Count > PreviewTaskCount;

            RefreshVisibleTaskCollections();
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, GenericLoadErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ShowAllIndividualTasksAsync()
    {
        return NavigationService.GoToRootAsync(AppRoutes.AllIndividualTasks);
    }

    [RelayCommand]
    private Task ShowAllGroupTasksAsync()
    {
        if (!CanShowAllGroupTasks)
        {
            return Task.CompletedTask;
        }

        isShowingAllGroupTasks = true;
        CanShowAllGroupTasks = false;
        RefreshVisibleTaskCollections();
        return Task.CompletedTask;
    }

    private async Task<List<CompanyTaskDto>> LoadAllIndividualTasksAsync(Guid userId)
    {
        var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, TasksPageSize);
        var allTasks = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;
        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : TasksPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));

        for (var page = 2; page <= totalPages; page++)
        {
            var pageResult = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, page, effectivePageSize);
            var pageItems = pageResult?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);
        }

        return allTasks.Select(TaskHelper.MapIndividualTask).ToList();
    }

    private async Task<List<CompanyTaskDto>> LoadAllGroupTasksSafeAsync(Guid userId, Guid? companyId)
    {
        if (companyId is null || companyId.Value == Guid.Empty)
        {
            return [];
        }

        try
        {
            return await LoadAllGroupTasksAsync(userId, companyId.Value);
        }
        catch (ApiException)
        {
            return [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }

    private async Task<List<CompanyTaskDto>> LoadAllGroupTasksAsync(Guid userId, Guid companyId)
    {
        var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
        var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);

        await Task.WhenAll(usersTask, groupsTask);

        var users = await usersTask ?? [];
        var groups = await groupsTask ?? [];
        var userNameMap = UserHelper.BuildUserNameMap(users);
        var userIdByNameMap = UserHelper.BuildUserIdByNameMap(users);
        var userGroups = GroupHelper.ResolveUserGroups(groups, userId, userNameMap);
        var groupMemberIds = GroupHelper.ResolveGroupMemberIds(userGroups, userIdByNameMap);

        if (groupMemberIds.Count == 0)
        {
            return [];
        }

        var firstPage = await projectManagementApiClient.GetGroupTasksByAssignedUsersAsync(groupMemberIds, 1, TasksPageSize);
        var allTasks = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;
        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : TasksPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));

        for (var page = 2; page <= totalPages; page++)
        {
            var pageResult = await projectManagementApiClient.GetGroupTasksByAssignedUsersAsync(groupMemberIds, page, effectivePageSize);
            var pageItems = pageResult?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);
        }

        return allTasks
            .Select(TaskHelper.NormalizeCategoryAndPriority)
            .ToList();
    }

    private void RefreshVisibleTaskCollections()
    {
        IndividualTasks.Clear();
        GroupTasks.Clear();

        var individualSource = isShowingAllIndividualTasks
            ? allIndividualTasks
            : allIndividualTasks.Take(PreviewTaskCount);

        var groupSource = isShowingAllGroupTasks
            ? allGroupTasks
            : allGroupTasks.Take(PreviewTaskCount);

        foreach (var task in individualSource)
        {
            IndividualTasks.Add(task);
        }

        foreach (var task in groupSource)
        {
            GroupTasks.Add(task);
        }
    }

}
