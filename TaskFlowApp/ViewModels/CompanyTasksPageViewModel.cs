using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Infrastructure.Helpers;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class CompanyTasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    IdentityApiClient identityApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int TasksPageSize = 100;
    private const int MaxPageTraversal = 200;
    private const int PreviewTaskCount = 4;

    private readonly List<CompanyTaskDto> allIndividualTasks = [];
    private readonly List<CompanyTaskDto> allGroupTasks = [];
    private bool isShowingAllIndividualTasks;
    private bool isShowingAllGroupTasks;

    public ObservableCollection<CompanyTaskDto> Tasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> IndividualTasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> GroupTasks { get; } = [];

    [ObservableProperty]
    private int totalTaskCount;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private int overdueTaskCount;

    [ObservableProperty]
    private bool canShowAllIndividualTasks;

    [ObservableProperty]
    private bool canShowAllGroupTasks;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var companyId = UserSession.CompanyId.Value;
            var groupedTasksTask = LoadAllCompanyGroupTasksSafeAsync(companyId);
            var individualTasksTask = LoadAllCompanyIndividualTasksAsync(companyId);

            await Task.WhenAll(groupedTasksTask, individualTasksTask);

            var groupedItems = await groupedTasksTask;
            var individualItems = await individualTasksTask;
            var items = groupedItems
                .Concat(individualItems)
                .ToList();

            Tasks.Clear();
            allIndividualTasks.Clear();
            allGroupTasks.Clear();
            foreach (var item in groupedItems)
            {
                Tasks.Add(item);
                allGroupTasks.Add(item);
            }

            foreach (var item in individualItems)
            {
                Tasks.Add(item);
                allIndividualTasks.Add(item);
            }

            isShowingAllIndividualTasks = false;
            isShowingAllGroupTasks = false;
            CanShowAllIndividualTasks = allIndividualTasks.Count > PreviewTaskCount;
            CanShowAllGroupTasks = allGroupTasks.Count > PreviewTaskCount;
            RefreshVisibleTaskCollections();

            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            TotalTaskCount = items.Count;

            int openCount = 0, overdueCount = 0;
            foreach (var item in items)
            {
                if (!TaskStatusHelper.IsCompletedStatus(item.StatusName))
                {
                    openCount++;
                    if (item.DeadlineTime < now)
                    {
                        overdueCount++;
                    }
                }
            }

            OpenTaskCount = openCount;
            OverdueTaskCount = overdueCount;

            StatusText = $"Toplam: {TotalTaskCount} | Açık: {OpenTaskCount} | Geciken: {OverdueTaskCount}";
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
            ErrorMessage = "Bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task ShowAllIndividualTasksAsync()
    {
        if (!CanShowAllIndividualTasks)
        {
            return Task.CompletedTask;
        }

        isShowingAllIndividualTasks = true;
        CanShowAllIndividualTasks = false;
        RefreshVisibleTaskCollections();
        return Task.CompletedTask;
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


    private async Task<List<CompanyTaskDto>> LoadAllCompanyGroupTasksAsync(Guid companyId)
    {
        var allTasks = new List<CompanyTaskDto>();
        var pageNumber = 1;

        while (pageNumber <= MaxPageTraversal)
        {
            var response = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, pageNumber, TasksPageSize);
            var pageItems = response?.Items ?? [];

            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);

            if (pageItems.Count < TasksPageSize)
            {
                break;
            }

            pageNumber++;
        }

        return allTasks
            .Select(TaskHelper.NormalizeCategoryAndPriority)
            .Select(task => task with { CategoryName = "Grup" })
            .ToList();
    }

    private async Task<List<CompanyTaskDto>> LoadAllCompanyGroupTasksSafeAsync(Guid companyId)
    {
        try
        {
            return await LoadAllCompanyGroupTasksAsync(companyId);
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

    private async Task<List<CompanyTaskDto>> LoadAllCompanyIndividualTasksAsync(Guid companyId)
    {
        var users = await identityApiClient.GetAllCompanyUsersAsync(companyId) ?? [];
        if (users.Count == 0)
        {
            return [];
        }

        var loadTasksByUserJobs = users
            .Where(user => user.Id != Guid.Empty)
            .Select(user => LoadIndividualTasksByUserIdSafeAsync(user.Id))
            .ToList();

        var tasksByUser = await Task.WhenAll(loadTasksByUserJobs);
        return tasksByUser
            .SelectMany(tasks => tasks)
            .GroupBy(
                task => (task.TaskName, task.Description, task.DeadlineTime, task.StatusName, task.CategoryName, task.TaskPriorityName))
            .Select(group => group.First())
            .ToList();
    }

    private async Task<List<CompanyTaskDto>> LoadIndividualTasksByUserIdSafeAsync(Guid userId)
    {
        try
        {
            var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, TasksPageSize);
            var allTasks = firstPage?.Items?.ToList() ?? [];
            var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;

            if (allTasks.Count < totalCount)
            {
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
            }

            return allTasks.Select(TaskHelper.MapIndividualTask).ToList();
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
