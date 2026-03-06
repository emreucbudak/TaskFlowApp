using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Models.Stats;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class DashBoardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    StatsApiClient statsApiClient,
    ProjectManagementApiClient projectManagementApiClient,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int GroupActivityPreviewCount = 5;
    private const int GroupMessagesPageSize = 100;
    private const int IndividualTasksPageSize = 100;
    private const string NoGroupMessage = "Üyesi olunan grup bulunamadı.";
    private const string NoActivityMessage = "Grupta henüz aktivite yok.";
    private const string NoDailySummaryMessage = "Günün özeti bulunamadı.";

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

    partial void OnHasGroupRecentActivitiesChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoGroupRecentActivities));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is null)
        {
            ErrorMessage = "Oturum bilgisi eksik. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            ResetGroupActivitiesState();

            var userId = UserSession.UserId.Value;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var individualTasksTask = LoadAllIndividualTasksAsync(userId);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);
            var groupActivitiesTask = LoadGroupRecentActivitiesSafeAsync(userId);

            WorkerStatsDto? stats = null;
            try
            {
                stats = await statsApiClient.GetWorkerStatsByUserAndPeriodAsync(userId, today);
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                // Bu ay icin istatistik kaydi olmayabilir; dashboard yine de acilmalidir.
                stats = null;
            }

            await Task.WhenAll(individualTasksTask, unreadTask, groupActivitiesTask);

            var individualTasks = await individualTasksTask;
            var unread = await unreadTask;
            var individualTaskCount = individualTasks.Count;
            var assignedTasks = stats?.TotalTasksAssigned ?? individualTaskCount;
            var groupedTasks = Math.Max(0, assignedTasks - individualTaskCount);
            var completedTasks = stats?.TotalTasksCompleted ?? 0;
            var overdueTaskCount = stats?.OverdueIncompleteTasksCount ?? 0;
            var completedIndividualTasks = individualTasks.Count(task => IsCompletedStatus(task.StatusName));
            var overdueIndividualTasks = individualTasks.Count(task =>
                task.Deadline < today &&
                !IsCompletedStatus(task.StatusName));

            TotalAssigned = assignedTasks;
            TotalCompleted = completedTasks;
            OverdueTasks = overdueTaskCount;
            IndividualTaskCount = individualTaskCount;
            GroupTaskCount = groupedTasks;
            CompletedIndividualTaskCount = completedIndividualTasks;
            CompletedGroupTaskCount = Math.Max(0, completedTasks - completedIndividualTasks);
            OverdueIndividualTaskCount = overdueIndividualTasks;
            OverdueGroupTaskCount = Math.Max(0, overdueTaskCount - overdueIndividualTasks);
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

    private async Task LoadGroupRecentActivitiesSafeAsync(Guid userId)
    {
        try
        {
            await LoadGroupRecentActivitiesAsync(userId);
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

    private async Task LoadGroupRecentActivitiesAsync(Guid userId)
    {
        if (UserSession.CompanyId is null || UserSession.CompanyId.Value == Guid.Empty)
        {
            ApplyNoGroupState();
            return;
        }

        var companyId = UserSession.CompanyId.Value;
        var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
        var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);

        await Task.WhenAll(groupsTask, usersTask);

        var groups = NormalizeGroups(await groupsTask ?? []);
        var users = await usersTask ?? [];

        var userNameMap = users
            .Where(user => user.Id != Guid.Empty && !string.IsNullOrWhiteSpace(user.Name))
            .GroupBy(user => user.Id)
            .ToDictionary(group => group.Key, group => group.First().Name.Trim());

        userNameMap.TryGetValue(userId, out var currentUserName);

        var currentGroup = groups.FirstOrDefault(group => IsGroupMember(group, userId, currentUserName));
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

        var messages = await chatApiClient.GetMessagesByGroupIdQueryRequestAsync(new
        {
            CurrentUserId = userId,
            GroupId = currentGroup.GroupId,
            PageSize = GroupMessagesPageSize,
            Page = 1
        }) ?? [];

        DailySummaryText = ResolveDailySummaryText(messages);

        var recentActivities = messages
            .Where(message => !message.IsDeleted)
            .OrderByDescending(message => message.SendTime)
            .Take(GroupActivityPreviewCount)
            .Select(message => new GroupRecentActivityItem
            {
                ActorName = ResolveDisplayName(message.SenderId, userNameMap),
                ActionText = ResolveActivityText(message.Content),
                OccurredAtText = FormatRelativeTime(message.SendTime)
            })
            .ToList();

        GroupRecentActivities.Clear();
        foreach (var activity in recentActivities)
        {
            GroupRecentActivities.Add(activity);
        }

        HasGroupRecentActivities = GroupRecentActivities.Count > 0;
        GroupActivityEmptyMessage = HasGroupRecentActivities ? string.Empty : NoActivityMessage;
    }

    private void ResetGroupActivitiesState()
    {
        GroupRecentActivities.Clear();
        HasGroupRecentActivities = false;
        HasUserGroup = false;
        CurrentGroupName = string.Empty;
        DailySummaryText = NoDailySummaryMessage;
        GroupActivityEmptyMessage = NoGroupMessage;
    }

    private void ApplyNoGroupState()
    {
        GroupRecentActivities.Clear();
        HasGroupRecentActivities = false;
        HasUserGroup = false;
        CurrentGroupName = string.Empty;
        DailySummaryText = NoDailySummaryMessage;
        GroupActivityEmptyMessage = NoGroupMessage;
    }

    private async Task<List<IndividualTaskDto>> LoadAllIndividualTasksAsync(Guid userId)
    {
        var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(
            userId,
            1,
            IndividualTasksPageSize);

        var allTasks = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;
        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : IndividualTasksPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));

        for (var page = 2; page <= totalPages; page++)
        {
            var pageResult = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(
                userId,
                page,
                effectivePageSize);
            var pageItems = pageResult?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);
        }

        return allTasks;
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
                    .SelectMany(group => group.WorkerUserIds ?? [])
                    .Where(workerId => workerId != Guid.Empty)
                    .Distinct()
                    .ToList(),
                WorkerName = grouped
                    .SelectMany(group => group.WorkerName ?? [])
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DepartmenName = grouped
                    .SelectMany(group => group.DepartmenName ?? [])
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            })
            .OrderBy(group => group.GroupName)
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

    private static string ResolveActivityText(string content)
    {
        var normalizedContent = content?.Trim();
        return string.IsNullOrWhiteSpace(normalizedContent)
            ? "gruba bir mesaj paylasti."
            : $"mesaj paylasti: {normalizedContent}";
    }

    private static string ResolveDailySummaryText(IEnumerable<MessageDto> messages)
    {
        foreach (var message in messages
                     .Where(message => !message.IsDeleted)
                     .OrderByDescending(message => message.SendTime))
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
            "Günün özeti:",
            "Günün özeti",
            "Gunun ozeti:",
            "Gunun ozeti",
            "Gün özeti:",
            "Gün özeti",
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

            var trimmedSummary = content[prefix.Length..].TrimStart(' ', ':', '-', '–');
            return string.IsNullOrWhiteSpace(trimmedSummary) ? content : trimmedSummary;
        }

        return content;
    }

    private static bool ContainsDailySummaryKeyword(string content)
    {
        var normalized = content.Trim().ToLowerInvariant();
        return normalized.Contains("günün özeti")
            || normalized.Contains("gunun ozeti")
            || normalized.Contains("gün özeti")
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