using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Infrastructure.Helpers;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Services.State;
using TaskFlowApp.Infrastructure.Authorization;

namespace TaskFlowApp.ViewModels;

public partial class DashBoardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int TasksPageSize = 100;
    private const string LoadingDailySummaryMessage = "Gunun ozeti hazirlaniyor.";
    private const string NoDailySummaryMessage = "Gunun ozeti bulunamadi.";

    [ObservableProperty]
    private int totalAssigned;

    [ObservableProperty]
    private int totalCompleted;

    [ObservableProperty]
    private int overdueTasks;

    [ObservableProperty]
    private int individualTaskCount;

    [ObservableProperty]
    private int groupTaskCount;

    [ObservableProperty]
    private int unreadMessageCount;

    [ObservableProperty]
    private int completedIndividualTaskCount;

    [ObservableProperty]
    private int completedGroupTaskCount;

    [ObservableProperty]
    private int overdueIndividualTaskCount;

    [ObservableProperty]
    private int overdueGroupTaskCount;

    [ObservableProperty]
    private string dailySummaryText = NoDailySummaryMessage;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is null || UserSession.CompanyId is null)
        {
            ErrorMessage = "Oturum bilgisi eksik. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await LoadWorkerReportAccessStateAsync();

            var userId = UserSession.UserId.Value;
            var companyId = UserSession.CompanyId.Value;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (workerDashboardStateService.TryGetCachedDailySummary(out var cachedDailySummary))
            {
                DailySummaryText = cachedDailySummary;
            }
            else
            {
                DailySummaryText = LoadingDailySummaryMessage;
            }

            if (workerDashboardStateService.TryGetUnreadMessageCount(out var cachedUnreadMessageCount))
            {
                UnreadMessageCount = cachedUnreadMessageCount;
            }

            _ = LoadDailySummaryAsync();

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var individualTasksTask = LoadAllIndividualTasksAsync(userId);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);

            await Task.WhenAll(usersTask, groupsTask, individualTasksTask, unreadTask);

            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var individualTasks = await individualTasksTask;
            var unread = await unreadTask;

            var userNameMap = UserHelper.BuildUserNameMap(users);
            var userGroups = GroupHelper.ResolveUserGroups(groups, userId, userNameMap);
            var groupMemberIds = GroupHelper.ResolveGroupMemberIds(userGroups);
            var groupTasks = await LoadAllGroupTasksAsync(companyId, groupMemberIds);

            int completedIndividualTasks = 0, overdueIndividualTasks = 0;
            foreach (var task in individualTasks)
            {
                var isCompleted = TaskStatusHelper.IsCompletedStatus(task.StatusName);
                if (isCompleted)
                {
                    completedIndividualTasks++;
                }
                else if (task.Deadline < today)
                {
                    overdueIndividualTasks++;
                }
            }

            int completedGroupTasks = 0, overdueGroupTasks = 0;
            foreach (var task in groupTasks)
            {
                var isCompleted = TaskStatusHelper.IsCompletedStatus(task.StatusName);
                if (isCompleted)
                {
                    completedGroupTasks++;
                }
                else if (task.DeadlineTime < today)
                {
                    overdueGroupTasks++;
                }
            }

            IndividualTaskCount = individualTasks.Count;
            GroupTaskCount = groupTasks.Count;
            CompletedIndividualTaskCount = completedIndividualTasks;
            CompletedGroupTaskCount = completedGroupTasks;
            OverdueIndividualTaskCount = overdueIndividualTasks;
            OverdueGroupTaskCount = overdueGroupTasks;
            TotalAssigned = IndividualTaskCount + GroupTaskCount;
            TotalCompleted = CompletedIndividualTaskCount + CompletedGroupTaskCount;
            OverdueTasks = OverdueIndividualTaskCount + OverdueGroupTaskCount;
            UnreadMessageCount = unread;
            workerDashboardStateService.SetUnreadMessageCount(unread);

            StatusText = string.Empty;
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

    private async Task<List<IndividualTaskDto>> LoadAllIndividualTasksAsync(Guid userId)
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

        return allTasks;
    }

    private async Task<List<CompanyTaskDto>> LoadAllGroupTasksAsync(Guid companyId, IReadOnlyList<Guid> groupMemberIds)
    {
        if (groupMemberIds.Count == 0)
        {
            return [];
        }

        var memberIdSet = groupMemberIds.ToHashSet();
        var matchingTasks = new List<CompanyTaskDto>();
        var pageNumber = 1;
        var totalCount = int.MaxValue;

        while ((pageNumber - 1) * TasksPageSize < totalCount)
        {
            var response = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, pageNumber, TasksPageSize);
            var pageItems = response?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            totalCount = response?.TotalCount > 0 ? response.TotalCount : pageItems.Count;

            matchingTasks.AddRange(pageItems.Where(task =>
                task.SubTasks.Any(subTask => memberIdSet.Contains(subTask.AssignedUserId))));

            if (pageItems.Count < TasksPageSize)
            {
                break;
            }

            pageNumber++;
        }

        return matchingTasks
            .GroupBy(task => (task.TaskName, task.Description, task.DeadlineTime, task.StatusName, task.CategoryName, task.TaskPriorityName))
            .Select(group => group.First())
            .ToList();
    }


    private async Task LoadDailySummaryAsync()
    {
        try
        {
            var summary = await workerDashboardStateService.GetOrLoadDailySummaryAsync();
            DailySummaryText = !string.IsNullOrWhiteSpace(summary)
                ? summary
                : NoDailySummaryMessage;
        }
        catch (Exception ex)
        {
            LogSilentFailure(ex);
            DailySummaryText = NoDailySummaryMessage;
        }
    }

}
