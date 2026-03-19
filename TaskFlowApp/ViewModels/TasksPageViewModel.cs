using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
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

            var individualTasks = OrderTasks(await individualTasksTask);
            var groupTasks = OrderTasks(await groupTasksTask);

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

        return allTasks.Select(MapIndividualTask).ToList();
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
        var userNameMap = BuildUserNameMap(users);
        var userIdByNameMap = BuildUserIdByNameMap(users);
        var userGroups = ResolveUserGroups(groups, userId, userNameMap);
        var groupMemberIds = ResolveGroupMemberIds(userGroups, userIdByNameMap);

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
            .Select(NormalizeCategoryAndPriority)
            .ToList();
    }

    private static CompanyTaskDto MapIndividualTask(IndividualTaskDto task)
    {
        var statusName = string.IsNullOrWhiteSpace(task.StatusName) ? TaskStatusHelper.DefaultOpenStatus : task.StatusName;
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName) ? "Bireysel" : task.CategoryName;
        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName) ? "Belirtilmedi" : task.TaskPriorityName;

        return NormalizeCategoryAndPriority(new CompanyTaskDto
        {
            TaskName = task.TaskTitle,
            Description = task.Description,
            DeadlineTime = task.Deadline,
            StatusName = statusName,
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        });
    }

    private static bool IsPriorityValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() is "dusuk"
            or "orta"
            or "yuksek"
            or "low"
            or "medium"
            or "high";
    }

    private static CompanyTaskDto NormalizeCategoryAndPriority(CompanyTaskDto task)
    {
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName)
            ? "Bireysel"
            : task.CategoryName.Trim();

        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName)
            ? "Belirtilmedi"
            : task.TaskPriorityName.Trim();

        if (IsPriorityValue(categoryName))
        {
            if (!IsPriorityValue(priorityName))
            {
                priorityName = categoryName;
            }

            categoryName = "Bireysel";
        }

        return task with
        {
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        };
    }

    private static List<CompanyTaskDto> OrderTasks(IEnumerable<CompanyTaskDto> tasks)
    {
        return tasks
            .OrderBy(task => TaskStatusHelper.IsCompletedStatus(task.StatusName))
            .ThenBy(task => task.DeadlineTime)
            .ThenBy(task => task.TaskName, StringComparer.OrdinalIgnoreCase)
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

    private static IReadOnlyDictionary<Guid, string> BuildUserNameMap(IEnumerable<CompanyUserDto> users)
    {
        return users
            .Where(user => user.Id != Guid.Empty && !string.IsNullOrWhiteSpace(user.Name))
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First().Name.Trim());
    }

    private static IReadOnlyDictionary<string, Guid> BuildUserIdByNameMap(IEnumerable<CompanyUserDto> users)
    {
        return users
            .Where(user => user.Id != Guid.Empty && !string.IsNullOrWhiteSpace(user.Name))
            .GroupBy(user => user.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Id, StringComparer.OrdinalIgnoreCase);
    }

    private static List<CompanyGroupDto> ResolveUserGroups(
        IEnumerable<CompanyGroupDto> groups,
        Guid userId,
        IReadOnlyDictionary<Guid, string> userNameMap)
    {
        userNameMap.TryGetValue(userId, out var currentUserName);

        return GroupHelper.NormalizeGroups(groups)
            .Where(group => GroupHelper.IsGroupMember(group, userId, currentUserName))
            .OrderBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<Guid> ResolveGroupMemberIds(
        IEnumerable<CompanyGroupDto> groups,
        IReadOnlyDictionary<string, Guid> userIdByNameMap)
    {
        var memberIds = new HashSet<Guid>(
            groups
                .SelectMany(group => group.WorkerUserIds)
                .Where(userId => userId != Guid.Empty));

        foreach (var workerName in groups.SelectMany(group => group.WorkerName))
        {
            var normalizedWorkerName = workerName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedWorkerName))
            {
                continue;
            }

            if (userIdByNameMap.TryGetValue(normalizedWorkerName, out var workerId) && workerId != Guid.Empty)
            {
                memberIds.Add(workerId);
            }
        }

        return memberIds.ToList();
    }

}