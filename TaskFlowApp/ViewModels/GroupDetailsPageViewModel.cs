using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class GroupDetailsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const string NoGroupMessage = "Uyesi olunan grup bulunamadi.";
    private const string NoMembersMessage = "Bu grup icin uye bilgisi bulunamadi.";

    public ObservableCollection<GroupDetailMemberItem> GroupMembers { get; } = [];
    public ObservableCollection<GroupActivityDisplayItem> Activities { get; } = [];

    private Guid currentGroupId;

    [ObservableProperty]
    private bool hasGroup;

    [ObservableProperty]
    private bool hasMembers;

    [ObservableProperty]
    private bool hasActivities;

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

    // Activity form fields
    [ObservableProperty]
    private bool isActivityFormVisible;

    [ObservableProperty]
    private string newActivityTitle = string.Empty;

    [ObservableProperty]
    private string newActivityDescription = string.Empty;

    [ObservableProperty]
    private bool isSubmittingActivity;

    public bool HasNoGroup => !HasGroup;
    public bool HasNoMembers => !HasMembers;
    public bool HasNoActivities => !HasActivities;

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

            // Check if current user is group leader
            CheckGroupLeaderStatus(currentGroup, userId);

            // Load activities for this group
            await LoadActivitiesAsync();

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

    private void CheckGroupLeaderStatus(CompanyGroupDto group, Guid userId)
    {
        IsGroupLeader = group.LeaderUserIds.Contains(userId);
    }

    private async Task LoadActivitiesAsync()
    {
        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            var activities = await identityApiClient.GetGroupActivitiesAsync(currentGroupId);
            Activities.Clear();

            if (activities is null || activities.Count == 0)
            {
                HasActivities = false;
                return;
            }

            foreach (var activity in activities)
            {
                Activities.Add(new GroupActivityDisplayItem
                {
                    ActivityId = activity.ActivityId,
                    Title = activity.Title,
                    Description = activity.Description,
                    SubmittedByUserName = activity.SubmittedByUserName,
                    SubmittedAtText = activity.SubmittedAt.ToString("dd.MM.yyyy HH:mm"),
                    Status = activity.Status,
                    StatusText = activity.StatusText,
                    StatusColor = activity.Status switch
                    {
                        0 => "#F59E0B", // Pending - amber
                        1 => "#10B981", // Approved - green
                        2 => "#EF4444", // Rejected - red
                        _ => "#64748B"
                    },
                    ReviewedByUserName = activity.ReviewedByUserName,
                    ReviewNote = activity.ReviewNote,
                    CanReview = IsGroupLeader && activity.Status == 0,
                    Initials = BuildInitials(activity.SubmittedByUserName)
                });
            }

            HasActivities = Activities.Count > 0;
        }
        catch
        {
            // Activities loading failure should not block the page
            HasActivities = false;
        }
    }

    [RelayCommand]
    private void ShowActivityForm()
    {
        IsActivityFormVisible = true;
        NewActivityTitle = string.Empty;
        NewActivityDescription = string.Empty;
    }

    [RelayCommand]
    private void HideActivityForm()
    {
        IsActivityFormVisible = false;
        NewActivityTitle = string.Empty;
        NewActivityDescription = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitActivityAsync()
    {
        if (string.IsNullOrWhiteSpace(NewActivityTitle))
        {
            ErrorMessage = "Aktivite basligi bos olamaz.";
            return;
        }

        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsSubmittingActivity = true;
            ErrorMessage = string.Empty;

            await identityApiClient.SubmitGroupActivityAsync(
                currentGroupId,
                NewActivityTitle.Trim(),
                NewActivityDescription?.Trim() ?? string.Empty);

            HideActivityForm();
            await LoadActivitiesAsync();
            StatusText = "Aktivite basariyla gonderildi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite gonderilemedi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Aktivite gonderilirken bir hata olustu.";
        }
        finally
        {
            IsSubmittingActivity = false;
        }
    }

    [RelayCommand]
    private async Task ApproveActivityAsync(Guid activityId)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await identityApiClient.ApproveGroupActivityAsync(activityId);
            await LoadActivitiesAsync();
            StatusText = "Aktivite onaylandi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite onaylanamadi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Onaylama sirasinda bir hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RejectActivityAsync(Guid activityId)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await identityApiClient.RejectGroupActivityAsync(activityId);
            await LoadActivitiesAsync();
            StatusText = "Aktivite reddedildi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite reddedilemedi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Reddetme sirasinda bir hata olustu.";
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

    private void ResetState()
    {
        GroupName = string.Empty;
        GroupDepartmentsText = string.Empty;
        GroupMembersSummaryText = string.Empty;
        GroupEmptyMessage = NoGroupMessage;
        MembersEmptyMessage = NoMembersMessage;
        GroupMembers.Clear();
        Activities.Clear();
        HasGroup = false;
        HasMembers = false;
        HasActivities = false;
        IsGroupLeader = false;
        IsActivityFormVisible = false;
        currentGroupId = Guid.Empty;
        StatusText = string.Empty;
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
                    .ToList(),
                LeaderUserIds = grouped
                    .SelectMany(group => group.LeaderUserIds)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
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

}

public sealed record GroupDetailMemberItem
{
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
}

public sealed record GroupActivityDisplayItem
{
    public Guid ActivityId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SubmittedByUserName { get; init; } = string.Empty;
    public string SubmittedAtText { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#64748B";
    public string? ReviewedByUserName { get; init; }
    public string? ReviewNote { get; init; }
    public bool CanReview { get; init; }
    public string Initials { get; init; } = string.Empty;
    public bool HasReviewNote => !string.IsNullOrWhiteSpace(ReviewNote);
    public string ReviewNoteDisplay => HasReviewNote ? $"Not: {ReviewNote}" : string.Empty;
}
