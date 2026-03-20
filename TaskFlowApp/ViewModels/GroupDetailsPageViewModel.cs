using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Infrastructure.Helpers;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class GroupDetailsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient,
    ChatApiClient chatApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int RecentActivityPreviewCount = 5;
    private const int RecentActivityPageSize = 20;
    private const string NoGroupMessage = "Üyesi olunan grup bulunamadı.";
    private const string NoMembersMessage = "Bu grup için üye bilgisi bulunamadı.";
    private const string NoRecentActivityMessage = "Grupta henüz mesaj aktivitesi yok.";
    private const string NoGroupEventsMessage = "Yaklaşan etkinlik bulunamadı.";

    public ObservableCollection<GroupDetailMemberItem> GroupMembers { get; } = [];
    public ObservableCollection<GroupRecentActivityItem> RecentGroupActivities { get; } = [];
    public ObservableCollection<GroupActivityDisplayItem> Activities { get; } = [];
    public ObservableCollection<GroupEventDisplayItem> GroupEvents { get; } = [];
    public IReadOnlyList<string> EventTypeOptions { get; } = ["Duyuru", "Toplanti", "Hatirlatma"];

    private Guid currentGroupId;

    [ObservableProperty]
    private bool hasGroup;

    [ObservableProperty]
    private bool hasMembers;

    [ObservableProperty]
    private bool hasActivities;

    [ObservableProperty]
    private bool hasRecentGroupActivities;

    [ObservableProperty]
    private bool isGroupLeader;

    [ObservableProperty]
    private string groupName = string.Empty;

    [ObservableProperty]
    private string groupDepartmentsText = string.Empty;

    [ObservableProperty]
    private string groupMembersSummaryText = string.Empty;

    [ObservableProperty]
    private string groupEmptyMessage = NoGroupMessage;

    [ObservableProperty]
    private string membersEmptyMessage = NoMembersMessage;

    public bool HasNoGroup => !HasGroup;
    public bool HasNoMembers => !HasMembers;
    public bool HasNoActivities => !HasActivities;
    public bool HasNoRecentGroupActivities => !HasRecentGroupActivities;
    public bool HasGroupEvents => GroupEvents.Count > 0;
    public bool HasNoGroupEvents => GroupEvents.Count == 0;

    partial void OnHasGroupChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoGroup));
    }

    partial void OnHasMembersChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoMembers));
    }

    partial void OnHasActivitiesChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoActivities));
    }

    partial void OnHasRecentGroupActivitiesChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoRecentGroupActivities));
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
            ErrorMessage = "Oturum bilgisi eksik. Tekrar giriş yapın.";
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
            var userNameMap = UserHelper.BuildUserNameMap(users);
            var userGroups = GroupHelper.ResolveUserGroups(groups, userId, userNameMap);
            var currentGroup = userGroups.FirstOrDefault();

            if (currentGroup is null)
            {
                ApplyNoGroupState();
                return;
            }

            ApplyCurrentGroupState(currentGroup, userNameMap);
            CheckGroupLeaderStatus(currentGroup, userId);

            var activitiesTask = LoadActivitiesAsync();
            var recentActivitiesTask = LoadRecentGroupActivitiesAsync(userId, userNameMap);
            var groupEventsTask = LoadGroupEventsAsync();
            await Task.WhenAll(activitiesTask, recentActivitiesTask, groupEventsTask);

            StatusText = $"{GroupName} detayları yüklendi.";
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

    private void CheckGroupLeaderStatus(CompanyGroupDto group, Guid userId)
    {
        IsGroupLeader = group.LeaderUserIds.Contains(userId);
    }

    private void ResetState()
    {
        GroupName = string.Empty;
        GroupDepartmentsText = string.Empty;
        GroupMembersSummaryText = string.Empty;
        GroupEmptyMessage = NoGroupMessage;
        MembersEmptyMessage = NoMembersMessage;
        RecentGroupActivityEmptyMessage = NoRecentActivityMessage;
        GroupEventsEmptyMessage = NoGroupEventsMessage;
        GroupMembers.Clear();
        RecentGroupActivities.Clear();
        Activities.Clear();
        GroupEvents.Clear();
        HasGroup = false;
        HasMembers = false;
        HasActivities = false;
        HasRecentGroupActivities = false;
        IsGroupLeader = false;
        IsActivityFormVisible = false;
        IsGroupEventFormVisible = false;
        IsEventEndEnabled = false;
        currentGroupId = Guid.Empty;
        StatusText = string.Empty;
        OnPropertyChanged(nameof(HasGroupEvents));
        OnPropertyChanged(nameof(HasNoGroupEvents));
    }

    private void ApplyNoGroupState()
    {
        ResetState();
    }

    private void ApplyCurrentGroupState(CompanyGroupDto group, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        HasGroup = true;
        currentGroupId = group.GroupId;
        GroupName = group.GroupName.Trim();

        var departmentNames = group.DepartmenName.ToList();

        GroupDepartmentsText = string.Empty;

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
                Initials = BuildInitials(memberName, "U")
            });
        }

        HasMembers = GroupMembers.Count > 0;
        GroupMembersSummaryText = HasMembers
            ? $"{GroupMembers.Count} üye"
            : "Üye bilgisi bulunmuyor";
    }

    [RelayCommand]
    private Task NavigateBackAsync()
    {
        return NavigationService.GoBackAsync();
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
        // DateTime.Now is correct here: comparing against already-localized time
        var elapsed = DateTime.Now - localTime;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Az önce";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalMinutes)} dk önce";
        }

        if (elapsed < TimeSpan.FromDays(1))
        {
            return $"{Math.Max(1, (int)elapsed.TotalHours)} sa önce";
        }

        if (elapsed < TimeSpan.FromDays(7))
        {
            return $"{Math.Max(1, (int)elapsed.TotalDays)} gün önce";
        }

        return localTime.ToString("dd.MM.yyyy HH:mm");
    }

    private static string FormatDateTime(DateTime dateTime)
    {
        return ConvertToLocalTime(dateTime).ToString("dd.MM.yyyy HH:mm");
    }

    private static string ResolveDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        if (userNameMap.TryGetValue(userId, out var userName) && !string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        return "Bilinmeyen kullanıcı";
    }

}

public sealed record GroupDetailMemberItem
{
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
}
