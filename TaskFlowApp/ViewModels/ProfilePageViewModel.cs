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

public partial class ProfilePageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<ProfileDetailItem> DetailItems { get; } = [];

    [ObservableProperty]
    private string displayName = "TaskFlow Kullanicisi";

    [ObservableProperty]
    private string initials = "TF";

    [ObservableProperty]
    private string roleTitle = "Profil";

    [ObservableProperty]
    private string subtitle = string.Empty;

    [ObservableProperty]
    private string summaryText = "Hesap ozetin hazirlaniyor.";

    [ObservableProperty]
    private string insightText = "Profil drawer menu ile hizli erisim sunar.";

    [ObservableProperty]
    private string primaryMetricTitle = "Departman";

    [ObservableProperty]
    private string primaryMetricValue = "0";

    [ObservableProperty]
    private string secondaryMetricTitle = "Grup";

    [ObservableProperty]
    private string secondaryMetricValue = "0";

    [ObservableProperty]
    private string tertiaryMetricTitle = "Yonetim";

    [ObservableProperty]
    private string tertiaryMetricValue = "Kapali";

    [ObservableProperty]
    private bool hasDetails;

    [ObservableProperty]
    private string detailEmptyMessage = "Detay bilgisi bulunamadi.";

    public bool HasNoDetails => !HasDetails;

    partial void OnHasDetailsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoDetails));
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

            var companyId = UserSession.CompanyId.Value;
            var userId = UserSession.UserId.Value;

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var departmentsTask = identityApiClient.GetAllCompanyDepartmentsAsync(companyId);

            await Task.WhenAll(usersTask, groupsTask, departmentsTask);

            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var departments = await departmentsTask ?? [];
            var currentUser = users.FirstOrDefault(user => user.Id == userId);

            DisplayName = ResolveCurrentUserName(currentUser);
            Initials = BuildInitials(DisplayName);

            if (IsCompanyUser)
            {
                ApplyCompanyState(users, groups, departments);
            }
            else
            {
                ApplyWorkerState(users, groups, currentUser, userId);
            }

            HasDetails = DetailItems.Count > 0;
            StatusText = "Profil detaylari yuklendi.";
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

    private void ResetState()
    {
        DisplayName = CurrentUserDisplayName;
        Initials = BuildInitials(DisplayName);
        RoleTitle = AccountRoleLabel;
        Subtitle = CurrentUserSupportText;
        SummaryText = "Hesap ozetin hazirlaniyor.";
        InsightText = "Profil drawer menu ile hizli erisim sunar.";
        PrimaryMetricTitle = "Departman";
        PrimaryMetricValue = "0";
        SecondaryMetricTitle = "Grup";
        SecondaryMetricValue = "0";
        TertiaryMetricTitle = "Yonetim";
        TertiaryMetricValue = IsCompanyUser ? "Acik" : "Kapali";
        DetailEmptyMessage = "Detay bilgisi bulunamadi.";
        DetailItems.Clear();
        HasDetails = false;
        StatusText = string.Empty;
    }

    private void ApplyCompanyState(
        IReadOnlyCollection<CompanyUserDto> users,
        IEnumerable<CompanyGroupDto> groups,
        IEnumerable<DepartmentDto> departments)
    {
        var normalizedGroups = NormalizeGroups(groups);
        var departmentNames = departments
            .Select(department => department.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RoleTitle = "Sirket yonetim hesabi";
        Subtitle = !string.IsNullOrWhiteSpace(UserSession.Email)
            ? UserSession.Email!
            : $"{users.Count} calisan kaydi bulundu";
        SummaryText = $"{users.Count} calisan, {departmentNames.Count} departman ve {normalizedGroups.Count} grup tek ekranda ozetlendi.";
        InsightText = "Raporlar ve abonelikler artik sagdan acilan profil menusunde toplanir.";

        PrimaryMetricTitle = "Calisan";
        PrimaryMetricValue = users.Count.ToString();
        SecondaryMetricTitle = "Departman";
        SecondaryMetricValue = departmentNames.Count.ToString();
        TertiaryMetricTitle = "Grup";
        TertiaryMetricValue = normalizedGroups.Count.ToString();

        AddDetail("Rol", RoleTitle);
        AddDetail("E-posta", ResolveEmailText());
        AddDetail("Kullanici", DisplayName);
        AddDetail("Sirket Kimligi", UserSession.CompanyId?.ToString() ?? "Bulunamadi");
        AddDetail("Kullanici Kimligi", UserSession.UserId?.ToString() ?? "Bulunamadi");
        AddDetail("Departmanlar", BuildJoinedSummary(departmentNames, "Kayitli departman yok."));
    }

    private void ApplyWorkerState(
        IReadOnlyCollection<CompanyUserDto> users,
        IEnumerable<CompanyGroupDto> groups,
        CompanyUserDto? currentUser,
        Guid userId)
    {
        var departmentNames = currentUser?.DepartmentMemberships
            .Select(membership => membership.DepartmentName?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        var userNameMap = BuildUserNameMap(users);
        var userGroups = ResolveUserGroups(groups, userId, userNameMap);
        var groupNames = userGroups
            .Select(group => group.GroupName.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        RoleTitle = CanAccessReportsPage
            ? "Departman lideri ve calisan"
            : "Calisan hesabi";
        Subtitle = !string.IsNullOrWhiteSpace(UserSession.Email)
            ? UserSession.Email!
            : departmentNames.Count == 0
                ? "Departman atamasi bulunmuyor"
                : BuildJoinedSummary(departmentNames, "Departman atamasi bulunmuyor");
        SummaryText = groupNames.Count == 0
            ? "Profilin hazir. Grup atamasi oldugunda burada gorunur."
            : $"{groupNames.Count} grup ve {departmentNames.Count} departman uyeligi bulundu.";
        InsightText = groupNames.Count == 0
            ? "Grubum sayfasi yine profil menusunden acilabilir."
            : $"Drawer uzerindeki Grubum kisayolu ile {groupNames[0]} detayina hizli donebilirsin.";

        PrimaryMetricTitle = "Departman";
        PrimaryMetricValue = departmentNames.Count.ToString();
        SecondaryMetricTitle = "Grup";
        SecondaryMetricValue = groupNames.Count.ToString();
        TertiaryMetricTitle = "Yonetim";
        TertiaryMetricValue = CanAccessReportsPage ? "Acik" : "Kapali";

        AddDetail("Rol", RoleTitle);
        AddDetail("E-posta", ResolveEmailText());
        AddDetail("Departmanlar", BuildJoinedSummary(departmentNames, "Departman atamasi bulunmuyor."));
        AddDetail("Grup Uyelikleri", BuildJoinedSummary(groupNames, "Aktif grup uyeligi bulunmuyor."));
        AddDetail("Kullanici Kimligi", UserSession.UserId?.ToString() ?? "Bulunamadi");
    }

    private string ResolveCurrentUserName(CompanyUserDto? currentUser)
    {
        var name = currentUser?.Name?.Trim();
        return string.IsNullOrWhiteSpace(name) ? CurrentUserDisplayName : name;
    }

    private string ResolveEmailText()
    {
        return string.IsNullOrWhiteSpace(UserSession.Email)
            ? "Kayitli e-posta bulunamadi."
            : UserSession.Email!;
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

    private static string BuildJoinedSummary(IReadOnlyList<string> items, string emptyText)
    {
        if (items.Count == 0)
        {
            return emptyText;
        }

        const int previewCount = 3;
        var preview = string.Join(", ", items.Take(previewCount));
        return items.Count > previewCount
            ? $"{preview}, +{items.Count - previewCount} diger"
            : preview;
    }
}

public sealed record ProfileDetailItem
{
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}
