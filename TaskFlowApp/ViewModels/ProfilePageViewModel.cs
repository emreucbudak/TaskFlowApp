using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class ProfilePageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int DepartmentLeaderRoleId = 1;

    public ObservableCollection<ProfileDetailItem> DetailItems { get; } = [];

    [ObservableProperty]
    private string displayName = "TaskFlow Kullanıcısı";

    [ObservableProperty]
    private string initials = "TF";

    [ObservableProperty]
    private string roleTitle = "Profil";

    [ObservableProperty]
    private string subtitle = string.Empty;

    [ObservableProperty]
    private string primaryMetricTitle = "Departman";

    [ObservableProperty]
    private string primaryMetricValue = "Henüz atanmadı";

    [ObservableProperty]
    private string secondaryMetricTitle = "Grup";

    [ObservableProperty]
    private string secondaryMetricValue = "Henüz yok";

    [ObservableProperty]
    private bool hasDetails;

    [ObservableProperty]
    private string detailEmptyMessage = "Ek profil bilgisi bulunamadı.";

    public bool HasNoDetails => !HasDetails;
    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);

    partial void OnHasDetailsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoDetails));
    }

    partial void OnSubtitleChanged(string value)
    {
        OnPropertyChanged(nameof(HasSubtitle));
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

            var accessState = await LoadWorkerReportAccessStateAsync();
            ResetState();

            var companyId = UserSession.CompanyId.Value;
            var userId = UserSession.UserId.Value;

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var departmentsTask = identityApiClient.GetAllCompanyDepartmentsAsync(companyId);

            await Task.WhenAll(usersTask, groupsTask, departmentsTask);

            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var departments = await departmentsTask ?? [];
            var currentUser = ResolveCurrentUser(users, userId);

            DisplayName = ResolveCurrentUserName(currentUser);
            Initials = BuildInitials(DisplayName);

            if (IsCompanyUser)
            {
                ApplyCompanyState(groups, departments);
            }
            else
            {
                ApplyWorkerState(users, groups, currentUser, userId, accessState);
            }

            HasDetails = DetailItems.Count > 0;
            StatusText = "Profil detayları yüklendi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Veriler şu anda yüklenemiyor. Lütfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Şu anda işlem gerçekleştirilemiyor. Lütfen tekrar deneyin.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "Şu anda işlem gerçekleştirilemiyor. Lütfen tekrar deneyin.";
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
    private Task NavigateBackAsync()
    {
        return NavigationService.GoBackAsync();
    }

    private void ResetState()
    {
        DisplayName = CurrentUserDisplayName;
        Initials = BuildInitials(DisplayName);
        RoleTitle = AccountRoleLabel;
        Subtitle = !string.IsNullOrWhiteSpace(UserSession.Email)
            ? UserSession.Email!.Trim()
            : string.Empty;
        PrimaryMetricTitle = "Departman";
        PrimaryMetricValue = "Henüz atanmadı";
        SecondaryMetricTitle = "Grup";
        SecondaryMetricValue = "Henüz yok";
        DetailEmptyMessage = "Ek profil bilgisi bulunamadı.";
        DetailItems.Clear();
        HasDetails = false;
        StatusText = string.Empty;
    }

    private void ApplyCompanyState(
        IEnumerable<CompanyGroupDto> groups,
        IEnumerable<DepartmentDto> departments)
    {
        var normalizedGroups = NormalizeGroups(groups);
        var departmentNames = NormalizeNames(departments.Select(department => department.Name));
        var groupNames = NormalizeNames(normalizedGroups.Select(group => group.GroupName));

        RoleTitle = "Şirket yönetim hesabı";
        Subtitle = !string.IsNullOrWhiteSpace(UserSession.Email)
            ? UserSession.Email!.Trim()
            : string.Empty;
        PrimaryMetricValue = BuildCardValue(departmentNames, "Kayıtlı departman yok");
        SecondaryMetricValue = BuildCardValue(groupNames, "Kayıtlı grup yok");

        AddDetail("Rol", RoleTitle);
        AddDetail("Şirket Kimliği", UserSession.CompanyId?.ToString() ?? "Bulunamadı");
        AddDetail("Departman", BuildJoinedSummary(departmentNames, "Kayıtlı departman yok."));
        AddDetail("Grup", BuildJoinedSummary(groupNames, "Kayıtlı grup yok."));
    }

    private void ApplyWorkerState(
        IReadOnlyCollection<CompanyUserDto> users,
        IEnumerable<CompanyGroupDto> groups,
        CompanyUserDto? currentUser,
        Guid userId,
        WorkerReportAccessState accessState)
    {
        var departmentNames = ResolveDepartmentNames(currentUser, accessState);
        var userNameMap = BuildUserNameMap(users);
        var userGroups = ResolveUserGroups(groups, userId, userNameMap);
        var groupNames = NormalizeNames(userGroups.Select(group => group.GroupName));
        var leaderDepartmentName = ResolveLeaderDepartmentName(currentUser, accessState);

        RoleTitle = CanAccessReportsPage
            ? BuildLeaderRoleTitle(leaderDepartmentName)
            : "Çalışan hesabı";
        Subtitle = !string.IsNullOrWhiteSpace(UserSession.Email)
            ? UserSession.Email!.Trim()
            : string.Empty;
        PrimaryMetricValue = BuildCardValue(departmentNames, "Henüz atanmadı");
        SecondaryMetricValue = BuildCardValue(groupNames, "Henüz yok");

        AddDetail("Rol", RoleTitle);
        AddDetail("Departman", BuildJoinedSummary(departmentNames, "Henüz bir departman üyeliği görünmüyor."));
        AddDetail("Grup", BuildJoinedSummary(groupNames, "Aktif grup üyeliği bulunmuyor."));
    }

    private CompanyUserDto? ResolveCurrentUser(IReadOnlyCollection<CompanyUserDto> users, Guid userId)
    {
        var currentUser = users.FirstOrDefault(user => user.Id == userId);
        if (currentUser is not null)
        {
            return currentUser;
        }

        var sessionDisplayName = UserSession.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(sessionDisplayName))
        {
            return null;
        }

        return users.FirstOrDefault(user =>
            string.Equals(user.Name?.Trim(), sessionDisplayName, StringComparison.OrdinalIgnoreCase));
    }

    private List<string> ResolveDepartmentNames(CompanyUserDto? currentUser, WorkerReportAccessState accessState)
    {
        var tokenDepartments = NormalizeNames(UserSession.DepartmentNames);
        var membershipDepartments = NormalizeNames(
            currentUser?.DepartmentMemberships.Select(membership => membership.DepartmentName)
            ?? Enumerable.Empty<string>());
        var leaderDepartments = NormalizeNames([accessState.DepartmentName]);

        return NormalizeNames(tokenDepartments.Concat(membershipDepartments).Concat(leaderDepartments));
    }

    private static string ResolveLeaderDepartmentName(CompanyUserDto? currentUser, WorkerReportAccessState accessState)
    {
        if (!string.IsNullOrWhiteSpace(accessState.DepartmentName))
        {
            return accessState.DepartmentName.Trim();
        }

        return currentUser?.DepartmentMemberships
            .Where(membership => membership.DepartmentRoleId == DepartmentLeaderRoleId)
            .Select(membership => membership.DepartmentName?.Trim())
            .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
            ?? string.Empty;
    }

    private static string BuildLeaderRoleTitle(string departmentName)
    {
        return string.IsNullOrWhiteSpace(departmentName)
            ? "Departman lideri"
            : $"{departmentName} Departmanı Lideri";
    }

    private string ResolveCurrentUserName(CompanyUserDto? currentUser)
    {
        var name = currentUser?.Name?.Trim();
        return string.IsNullOrWhiteSpace(name) ? CurrentUserDisplayName : name;
    }

    private void AddDetail(string title, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        DetailItems.Add(new ProfileDetailItem
        {
            Title = title,
            Value = value.Trim()
        });
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

    private static List<string> NormalizeNames(IEnumerable<string?> names)
    {
        return names
            .Select(name => name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildCardValue(IReadOnlyList<string> items, string emptyText)
    {
        if (items.Count == 0)
        {
            return emptyText;
        }

        return items.Count == 1
            ? items[0]
            : $"{items[0]} +{items.Count - 1}";
    }

    private static string BuildJoinedSummary(IReadOnlyList<string> items, string emptyText)
    {
        if (items.Count == 0)
        {
            return emptyText;
        }

        const int previewCount = 3;
        var preview = string.Join(", ", items.Take(previewCount));
        return items.Count > previewCount
            ? $"{preview}, +{items.Count - previewCount} diğer"
            : preview;
    }
}

public sealed record ProfileDetailItem
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
