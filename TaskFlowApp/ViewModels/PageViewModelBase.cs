using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public abstract partial class PageViewModelBase(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager) : ObservableObject
{
    private bool canAccessReportsPage;
    private string currentLeaderDepartmentName = string.Empty;

    protected IUserSession UserSession { get; } = userSession;
    protected INavigationService NavigationService { get; } = navigationService;
    protected IRealtimeConnectionManager RealtimeConnectionManager { get; } = realtimeConnectionManager;

    public bool CanAccessReportsPage
    {
        get => canAccessReportsPage;
        protected set
        {
            if (!SetProperty(ref canAccessReportsPage, value))
            {
                return;
            }

            OnPropertyChanged(nameof(ShowReportsNavigation));
            OnPropertyChanged(nameof(ShowLeaderManagementNavigation));
            OnPropertyChanged(nameof(AccountRoleLabel));
            OnPropertyChanged(nameof(CurrentUserRoleText));
            OnPropertyChanged(nameof(CurrentUserSupportText));
            OnPropertyChanged(nameof(HasCurrentUserSupportText));
        }
    }

    public bool IsCompanyUser => string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase);
    public bool IsWorkerUser => string.Equals(UserSession.Role, "worker", StringComparison.OrdinalIgnoreCase);
    public bool ShowReportsNavigation => IsCompanyUser || CanAccessReportsPage;
    public bool ShowLeaderManagementNavigation => IsWorkerUser && CanAccessReportsPage;
    public bool ShowGroupNavigation => IsWorkerUser;
    public string NotificationsMenuTitle => IsCompanyUser ? "Abonelikler" : "Bildirimler";
    public string NotificationsMenuDescription => IsCompanyUser
        ? "Planını ve abonelik detaylarını yönet."
        : "Bildirim akışını tek ekranda takip et.";
    public string AccountRoleLabel => IsCompanyUser
        ? "Şirket yönetim hesabı"
        : CanAccessReportsPage
            ? string.IsNullOrWhiteSpace(currentLeaderDepartmentName)
                ? "Departman lideri"
                : $"{currentLeaderDepartmentName} Departmanı Lideri"
            : "Çalışan hesabı";
    public string CurrentUserDisplayName => !string.IsNullOrWhiteSpace(UserSession.DisplayName)
        ? UserSession.DisplayName!.Trim()
        : IsCompanyUser
            ? "Şirket Yöneticisi"
            : "TaskFlow Kullanıcısı";
    public string CurrentUserRoleText => AccountRoleLabel;
    public string CurrentUserSupportText => !string.IsNullOrWhiteSpace(UserSession.Email)
        ? UserSession.Email!.Trim()
        : string.Empty;
    public bool HasCurrentUserSupportText => !string.IsNullOrWhiteSpace(CurrentUserSupportText);
    public string CurrentUserInitials => BuildInitials(CurrentUserDisplayName);

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool isProfileMenuOpen;

    protected const string GenericConnectionErrorMessage = "Su anda islem gerceklestirilemiyor. Lutfen tekrar deneyin.";
    protected const string GenericLoadErrorMessage = "Veriler su anda yuklenemiyor. Lutfen tekrar deneyin.";
    protected const string SessionExpiredMessage = "Oturumunuz sona erdi. Lutfen yeniden giris yapin.";
    protected const string AccessDeniedMessage = "Bu islem icin aktif abonelik veya yetkiniz bulunmuyor.";

    protected static string ResolveApiErrorMessage(ApiException exception, string defaultMessage)
    {
        return exception.StatusCode switch
        {
            401 => SessionExpiredMessage,
            403 => AccessDeniedMessage,
            _ => defaultMessage
        };
    }

    protected async Task<WorkerReportAccessState> LoadWorkerReportAccessStateAsync(CancellationToken cancellationToken = default)
    {
        if (!IsWorkerUser)
        {
            SetCurrentLeaderDepartmentName(string.Empty);
            CanAccessReportsPage = false;
            return WorkerReportAccessState.None;
        }

        try
        {
            var resolver = ServiceLocator.GetRequiredService<IWorkerReportAccessResolver>();
            var state = await resolver.GetStateAsync(cancellationToken);
            SetCurrentLeaderDepartmentName(state.DepartmentName);
            CanAccessReportsPage = state.CanAccessReportsPage;
            return state;
        }
        catch
        {
            SetCurrentLeaderDepartmentName(string.Empty);
            CanAccessReportsPage = false;
            return WorkerReportAccessState.None;
        }
    }

    [RelayCommand]
    private void ToggleProfileMenu()
    {
        IsProfileMenuOpen = !IsProfileMenuOpen;
    }

    [RelayCommand]
    private void CloseProfileMenu()
    {
        IsProfileMenuOpen = false;
    }

    [RelayCommand]
    private Task NavigateHomeAsync()
    {
        CloseProfileMenu();

        var homeRoute = IsCompanyUser
            ? "CompanyDashboardPage"
            : "DashBoardPage";

        return NavigationService.GoToRootAsync(homeRoute);
    }

    [RelayCommand]
    private Task NavigateReportsAsync()
    {
        CloseProfileMenu();

        var reportsRoute = IsCompanyUser
            ? "CompanyReportsPage"
            : "ReportsPage";

        return NavigationService.GoToRootAsync(reportsRoute);
    }

    [RelayCommand]
    private Task NavigateTasksAsync()
    {
        CloseProfileMenu();

        var tasksRoute = IsCompanyUser
            ? "CompanyTasksPage"
            : "TasksPage";

        return NavigationService.GoToRootAsync(tasksRoute);
    }

    [RelayCommand]
    private Task NavigateLeaderTasksAsync()
    {
        CloseProfileMenu();

        if (!IsWorkerUser || !CanAccessReportsPage)
        {
            return Task.CompletedTask;
        }

        return NavigationService.GoToRootAsync("LeaderIndividualTaskPage");
    }

    [RelayCommand]
    private Task NavigateMessagesAsync()
    {
        CloseProfileMenu();

        var employeesRoute = IsCompanyUser
            ? "CompanyEmployeesPage"
            : "MessagesPage";

        return NavigationService.GoToRootAsync(employeesRoute);
    }

    [RelayCommand]
    private Task NavigateNotificationsAsync()
    {
        CloseProfileMenu();

        var subscriptionsRoute = IsCompanyUser
            ? "CompanySubscriptionsPage"
            : "NotificationsPage";

        return NavigationService.GoToRootAsync(subscriptionsRoute);
    }

    [RelayCommand]
    private Task NavigateGroupDetailsAsync()
    {
        CloseProfileMenu();

        if (!IsWorkerUser)
        {
            return Task.CompletedTask;
        }

        return NavigationService.GoToAsync("GroupDetailsPage");
    }

    [RelayCommand]
    private Task NavigateProfileAsync()
    {
        CloseProfileMenu();
        return NavigationService.GoToAsync("ProfilePage");
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        CloseProfileMenu();
        await RealtimeConnectionManager.DisconnectAllAsync();
        SetCurrentLeaderDepartmentName(string.Empty);
        UserSession.Clear();
        CanAccessReportsPage = false;
        await NavigationService.GoToRootAsync("MainPage");
    }


    private void SetCurrentLeaderDepartmentName(string? departmentName)
    {
        var normalizedDepartmentName = departmentName?.Trim() ?? string.Empty;
        if (string.Equals(currentLeaderDepartmentName, normalizedDepartmentName, StringComparison.Ordinal))
        {
            return;
        }

        currentLeaderDepartmentName = normalizedDepartmentName;
        OnPropertyChanged(nameof(AccountRoleLabel));
        OnPropertyChanged(nameof(CurrentUserRoleText));
    }

    protected static string BuildInitials(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "TF";
        }

        var initials = string.Concat(value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

        return string.IsNullOrWhiteSpace(initials) ? "TF" : initials;
    }
}

