using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class GroupDetailsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int GroupMessagesPageSize = 100;
    private const string NoGroupMessage = "Uyesi olunan grup bulunamadi.";
    private const string NoActivityMessage = "Bu grupta henuz aktivite yok.";
    private const string NoDailySummaryMessage = "Gunun ozeti bulunamadi.";
    private const string NoMembersMessage = "Bu grup icin uye bilgisi bulunamadi.";

    public ObservableCollection<GroupDetailMemberItem> GroupMembers { get; } = [];
    public ObservableCollection<GroupRecentActivityItem> GroupActivities { get; } = [];

    [ObservableProperty]
    private bool hasGroup;

    [ObservableProperty]
    private bool hasActivities;

    [ObservableProperty]
    private bool hasMembers;

    [ObservableProperty]
    private string groupName = string.Empty;

    [ObservableProperty]
    private string groupDepartmentsText = string.Empty;

    [ObservableProperty]
    private string groupMembersSummaryText = string.Empty;

    [ObservableProperty]
    private string dailySummaryText = NoDailySummaryMessage;

    [ObservableProperty]
    private string groupEmptyMessage = NoGroupMessage;

    [ObservableProperty]
    private string activitiesEmptyMessage = NoActivityMessage;

    [ObservableProperty]
    private string membersEmptyMessage = NoMembersMessage;

    public bool HasNoGroup => !HasGroup;
    public bool HasNoActivities => !HasActivities;
    public bool HasNoMembers => !HasMembers;

    partial void OnHasGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoGroup));
    }

    partial void OnHasActivitiesChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoActivities));
    }

    partial void OnHasMembersChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoMembers));
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
            ResetState();

            var userId = UserSession.UserId.Value;
            var companyId = UserSession.CompanyId.Value;

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);

            await Task.WhenAll(usersTask, groupsTask);

            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var userNameMap = BuildUserNameMap(users);
            var userGroups = ResolveUserGroups(groups, userId, userNameMap);
            var currentGroup = userGroups.FirstOrDefault();

            if (currentGroup is null)
            {
                ApplyNoGroupState();
                return;
            }

            ApplyCurrentGroupState(currentGroup, userNameMap);

            if (currentGroup.GroupId == Guid.Empty)
            {
                StatusText = $"{GroupName} detaylari hazir.";
                return;
            }

            var messages = await LoadAllGroupMessagesAsync(userId, currentGroup.GroupId);
            ApplyMessagesState(messages, userNameMap);
            StatusText = $"{GroupName} detaylari yuklendi.";
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
    private Task NavigateBackAsync()
    {
        return NavigationService.GoBackAsync();
    }

    private async Task<List<MessageDto>> LoadAllGroupMessagesAsync(Guid userId, Guid groupId)
    {
        var allMessages = new List<MessageDto>();

        for (var page = 1; ; page++)
        {
            var pageItems = await chatApiClient.GetMessagesByGroupIdQueryRequestAsync(new
            {
                CurrentUserId = userId,
                GroupId = groupId,
                PageSize = GroupMessagesPageSize,
                Page = page
            }) ?? [];

            if (pageItems.Count == 0)
            {
                break;
            }

            allMessages.AddRange(pageItems);

            if (pageItems.Count < GroupMessagesPageSize)
            {
                break;
            }
        }

        return allMessages;
    }

    private void ResetState()
    {
        GroupName = string.Empty;
        GroupDepartmentsText = string.Empty;
        GroupMembersSummaryText = string.Empty;
        DailySummaryText = NoDailySummaryMessage;
        GroupEmptyMessage = NoGroupMessage;
        ActivitiesEmptyMessage = NoActivityMessage;
        MembersEmptyMessage = NoMembersMessage;
        GroupMembers.Clear();
        GroupActivities.Clear();
        HasGroup = false;
        HasActivities = false;
        HasMembers = false;
        StatusText = string.Empty;
    }

    private void ApplyNoGroupState()
    {
        ResetState();
    }

    private void ApplyCurrentGroupState(CompanyGroupDto group, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        HasGroup = true;
        GroupName = group.GroupName.Trim();

        var departmentNames = group.DepartmenName
            .Select(name => name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GroupDepartmentsText = departmentNames.Count == 0
            ? "Departman bilgisi bulunmuyor."
            : string.Join(", ", departmentNames);

        var memberNames = group.WorkerUserIds
            .Where(userId => userId != Guid.Empty)
            .Select(userId => ResolveDisplayName(userId, userNameMap))
            .Concat(group.WorkerName
                .Select(name => name?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Cast<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        GroupMembers.Clear();
        foreach (var memberName in memberNames)
        {
            GroupMembers.Add(new GroupDetailMemberItem
            {
                Name = memberName,
                Initials = BuildInitials(memberName)
            });
        }

        HasMembers = GroupMembers.Count > 0;
        GroupMembersSummaryText = HasMembers
            ? $"{GroupMembers.Count} uye"
            : "Uye bilgisi bulunmuyor";
    }

    private void ApplyMessagesState(IEnumerable<MessageDto> messages, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        var visibleMessages = messages
            .Where(message => !message.IsDeleted)
            .OrderByDescending(message => message.SendTime)
            .ToList();

        DailySummaryText = ResolveDailySummaryText(visibleMessages);

        GroupActivities.Clear();
        foreach (var message in visibleMessages)
        {
            GroupActivities.Add(new GroupRecentActivityItem
            {
                ActorName = ResolveDisplayName(message.SenderId, userNameMap),
                ActionText = ResolveActivityText(message.Content),
                OccurredAtText = FormatRelativeTime(message.SendTime)
            });
        }

        HasActivities = GroupActivities.Count > 0;
        ActivitiesEmptyMessage = HasActivities ? string.Empty : NoActivityMessage;
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
            .OrderBy(group => group.GroupName, StringComparer.OrdinalIgnoreCase)
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
                GroupName = grouped.First().GroupName.Trim(),
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

    private static string BuildInitials(string name)
    {
        var initials = string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

        return string.IsNullOrWhiteSpace(initials) ? "U" : initials;
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
        foreach (var message in messages.OrderByDescending(message => message.SendTime))
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

public sealed record GroupDetailMemberItem
{
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
}
