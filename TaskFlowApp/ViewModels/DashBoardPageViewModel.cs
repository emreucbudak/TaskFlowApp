using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class DashBoardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int GroupActivityPreviewCount = 5;
    private const int GroupSummaryPageSize = 100;
    private const int TasksPageSize = 100;
    private const string NoGroupMessage = "Uyesi olunan grup bulunamadi.";
    private const string NoActivityMessage = "Grupta henuz aktivite yok.";
    private const string NoDailySummaryMessage = "Gunun ozeti bulunamadi.";
    private readonly List<GroupRecentActivityItem> allGroupRecentActivities = [];
    public ObservableCollection<GroupRecentActivityItem> GroupRecentActivities { get; } = [];

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
    private string groupActivityEmptyMessage = NoGroupMessage;

    [ObservableProperty]
    private string currentGroupName = string.Empty;

    [ObservableProperty]
    private string dailySummaryText = NoDailySummaryMessage;

    [ObservableProperty]
    private bool hasGroupRecentActivities;

    [ObservableProperty]
    private bool hasUserGroup;

    public bool HasNoGroupRecentActivities => !HasGroupRecentActivities;
    public bool CanOpenGroupDetails => HasUserGroup;

    partial void OnHasGroupRecentActivitiesChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoGroupRecentActivities));
    }

    partial void OnHasUserGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(CanOpenGroupDetails));
    }

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
            ResetGroupActivitiesState();

            var userId = UserSession.UserId.Value;
            var companyId = UserSession.CompanyId.Value;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var individualTasksTask = LoadAllIndividualTasksAsync(userId);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);

            await Task.WhenAll(usersTask, groupsTask, individualTasksTask, unreadTask);

            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var individualTasks = await individualTasksTask;
            var unread = await unreadTask;

            var userNameMap = BuildUserNameMap(users);
            var userGroups = ResolveUserGroups(groups, userId, userNameMap);
            var groupMemberIds = ResolveGroupMemberIds(userGroups);

            var groupTasksTask = LoadAllGroupTasksAsync(companyId, groupMemberIds);
            var groupActivitiesTask = LoadGroupRecentActivitiesSafeAsync(userId, userGroups, userNameMap);

            await Task.WhenAll(groupTasksTask, groupActivitiesTask);

            var groupTasks = await groupTasksTask;
            var completedIndividualTasks = individualTasks.Count(task => IsCompletedStatus(task.StatusName));
            var completedGroupTasks = groupTasks.Count(task => IsCompletedStatus(task.StatusName));
            var overdueIndividualTasks = individualTasks.Count(task => task.Deadline < today && !IsCompletedStatus(task.StatusName));
            var overdueGroupTasks = groupTasks.Count(task => task.DeadlineTime < today && !IsCompletedStatus(task.StatusName));

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

    [RelayCommand]
    private Task OpenGroupDetailsAsync()
    {
        if (!HasUserGroup)
        {
            return Task.CompletedTask;
        }

        return NavigationService.GoToAsync("GroupDetailsPage");
    }

    private async Task LoadGroupRecentActivitiesSafeAsync(
        Guid userId,
        IReadOnlyList<CompanyGroupDto> userGroups,
        IReadOnlyDictionary<Guid, string> userNameMap)
    {
        try
        {
            await LoadGroupRecentActivitiesAsync(userId, userGroups, userNameMap);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            ApplyNoGroupState();
        }
        catch
        {
            if (!HasGroupRecentActivities)
            {
                GroupActivityEmptyMessage = NoActivityMessage;
            }
        }
    }

    private async Task LoadGroupRecentActivitiesAsync(
        Guid userId,
        IReadOnlyList<CompanyGroupDto> userGroups,
        IReadOnlyDictionary<Guid, string> userNameMap)
    {
        var currentGroup = userGroups.FirstOrDefault();
        if (currentGroup is null)
        {
            ApplyNoGroupState();
            return;
        }

        HasUserGroup = true;
        CurrentGroupName = currentGroup.GroupName;

        if (currentGroup.GroupId == Guid.Empty)
        {
            GroupActivityEmptyMessage = NoActivityMessage;
            DailySummaryText = NoDailySummaryMessage;
            return;
        }

        var summaryMessagesTask = chatApiClient.GetMessagesByGroupIdQueryRequestAsync(new
        {
            CurrentUserId = userId,
            GroupId = currentGroup.GroupId,
            PageSize = GroupSummaryPageSize,
            Page = 1
        });

        var summaryMessages = await summaryMessagesTask ?? [];

        DailySummaryText = ResolveDailySummaryText(summaryMessages);

        var recentActivities = summaryMessages
            .Where(message => !message.IsDeleted)
            .OrderByDescending(message => message.SendTime)
            .Select(message => new GroupRecentActivityItem
            {
                ActorName = ResolveDisplayName(message.SenderId, userNameMap),
                ActionText = ResolveActivityText(message.Content),
                OccurredAtText = FormatRelativeTime(message.SendTime)
            })
            .ToList();

        allGroupRecentActivities.Clear();
        allGroupRecentActivities.AddRange(recentActivities);
        RefreshVisibleGroupRecentActivities();
    }

    private void ResetGroupActivitiesState()
    {
        allGroupRecentActivities.Clear();
        GroupRecentActivities.Clear();
        HasGroupRecentActivities = false;
        HasUserGroup = false;
        CurrentGroupName = string.Empty;
        DailySummaryText = NoDailySummaryMessage;
        GroupActivityEmptyMessage = NoGroupMessage;
    }

    private void ApplyNoGroupState()
    {
        allGroupRecentActivities.Clear();
        GroupRecentActivities.Clear();
        HasGroupRecentActivities = false;
        HasUserGroup = false;
        CurrentGroupName = string.Empty;
        DailySummaryText = NoDailySummaryMessage;
        GroupActivityEmptyMessage = NoGroupMessage;
    }

    private void RefreshVisibleGroupRecentActivities()
    {
        GroupRecentActivities.Clear();
        foreach (var activity in allGroupRecentActivities.Take(GroupActivityPreviewCount))
        {
            GroupRecentActivities.Add(activity);
        }

        HasGroupRecentActivities = GroupRecentActivities.Count > 0;
        GroupActivityEmptyMessage = HasGroupRecentActivities ? string.Empty : NoActivityMessage;
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
            .GroupBy(task => $"{task.TaskName}|{task.Description}|{task.DeadlineTime}|{task.StatusName}|{task.CategoryName}|{task.TaskPriorityName}")
            .Select(group => group.First())
            .ToList();
    }

    private static IReadOnlyDictionary<Guid, string> BuildUserNameMap(IEnumerable<CompanyUserDto> users)
    {
        return users
            .Where(user => user.Id != Guid.Empty && !string.IsNullOrWhiteSpace(user.Name))
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First().Name.Trim());
    }

    private static List<CompanyGroupDto> ResolveUserGroups(
        IEnumerable<CompanyGroupDto> groups,
        Guid userId,
        IReadOnlyDictionary<Guid, string> userNameMap)
    {
        userNameMap.TryGetValue(userId, out var currentUserName);

        return NormalizeGroups(groups)
            .Where(group => IsGroupMember(group, userId, currentUserName))
            .OrderBy(group => group.GroupName)
            .ToList();
    }

    private static List<Guid> ResolveGroupMemberIds(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .SelectMany(group => group.WorkerUserIds)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();
    }

    private static bool IsGroupMember(CompanyGroupDto group, Guid userId, string? currentUserName)
    {
        if (group.WorkerUserIds.Contains(userId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            return false;
        }

        return group.WorkerName.Any(name =>
            string.Equals(name?.Trim(), currentUserName, StringComparison.OrdinalIgnoreCase));
    }

    private static List<CompanyGroupDto> NormalizeGroups(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupName))
            .GroupBy(group => group.GroupName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grouped => new CompanyGroupDto
            {
                GroupId = grouped
                    .Select(group => group.GroupId)
                    .FirstOrDefault(groupId => groupId != Guid.Empty),
                GroupName = grouped.First().GroupName,
                WorkerUserIds = grouped
                    .SelectMany(group => group.WorkerUserIds)
                    .Where(workerId => workerId != Guid.Empty)
                    .Distinct()
                    .ToList(),
                WorkerName = grouped
                    .SelectMany(group => group.WorkerName)
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DepartmenName = grouped
                    .SelectMany(group => group.DepartmenName)
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .ToList();
    }

    private static string ResolveDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        if (userNameMap.TryGetValue(userId, out var userName) && !string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return "Bilinmeyen kullanici";
    }

    private static string ResolveActivityText(string? content)
    {
        var normalizedContent = content?.Trim();
        return string.IsNullOrWhiteSpace(normalizedContent)
            ? "gruba bir mesaj paylasti."
            : $"mesaj paylasti: {normalizedContent}";
    }

    private static string ResolveDailySummaryText(IEnumerable<MessageDto> messages)
    {
        foreach (var message in messages.Where(message => !message.IsDeleted).OrderByDescending(message => message.SendTime))
        {
            var summaryText = TryExtractDailySummaryText(message);
            if (!string.IsNullOrWhiteSpace(summaryText))
            {
                return summaryText;
            }
        }

        return NoDailySummaryMessage;
    }

    private static string? TryExtractDailySummaryText(MessageDto message)
    {
        var localTime = ConvertToLocalTime(message.SendTime);
        if (localTime.Date != DateTime.Now.Date)
        {
            return null;
        }

        var content = message.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || !ContainsDailySummaryKeyword(content))
        {
            return null;
        }

        var prefixes = new[]
        {
            "Gunun ozeti:",
            "Gunun ozeti",
            "Gun ozeti:",
            "Gun ozeti",
            "Daily summary:",
            "Daily summary"
        };

        foreach (var prefix in prefixes)
        {
            if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var trimmedSummary = content[prefix.Length..].TrimStart(' ', ':', '-');
            return string.IsNullOrWhiteSpace(trimmedSummary) ? content : trimmedSummary;
        }

        return content;
    }

    private static bool ContainsDailySummaryKeyword(string content)
    {
        var normalized = content.Trim().ToLowerInvariant();
        return normalized.Contains("gunun ozeti")
            || normalized.Contains("gun ozeti")
            || normalized.Contains("daily summary");
    }

    private static bool IsCompletedStatus(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            return false;
        }

        var normalizedStatus = statusName.Trim().ToLowerInvariant();
        return normalizedStatus.Contains("tamam")
            || normalizedStatus.Contains("complete")
            || normalizedStatus.Contains("done")
            || normalizedStatus.Contains("closed");
    }

    private static DateTime ConvertToLocalTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };
    }

    private static string FormatRelativeTime(DateTime sendTime)
    {
        var localTime = ConvertToLocalTime(sendTime);
        var elapsed = DateTime.Now - localTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Az once";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} dk once";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)} sa once";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, (int)elapsed.TotalDays)} gun once";
        }

        return localTime.ToString("dd.MM.yyyy HH:mm");
    }
}
