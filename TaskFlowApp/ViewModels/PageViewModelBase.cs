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

    protected IUserSession UserSession { get; } = userSession;
    protected INavigationService NavigationService { get; } = navigationService;
    protected IRealtimeConnectionManager RealtimeConnectionManager { get; } = realtimeConnectionManager;

    public bool CanAccessReportsPage
    {
        get => canAccessReportsPage;
        protected set => SetProperty(ref canAccessReportsPage, value);
    }

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

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
        if (!string.Equals(UserSession.Role, "worker", StringComparison.OrdinalIgnoreCase))
        {
            CanAccessReportsPage = false;
            return WorkerReportAccessState.None;
        }

        try
        {
            var resolver = ServiceLocator.GetRequiredService<IWorkerReportAccessResolver>();
            var state = await resolver.GetStateAsync(cancellationToken);
            CanAccessReportsPage = state.CanAccessReportsPage;
            return state;
        }
        catch
        {
            CanAccessReportsPage = false;
            return WorkerReportAccessState.None;
        }
    }

    [RelayCommand]
    private Task NavigateHomeAsync()
    {
        var homeRoute = string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase)
            ? "CompanyDashboardPage"
            : "DashBoardPage";

        return NavigationService.GoToRootAsync(homeRoute);
    }

    [RelayCommand]
    private Task NavigateReportsAsync()
    {
        var reportsRoute = string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase)
            ? "CompanyReportsPage"
            : "ReportsPage";

        return NavigationService.GoToRootAsync(reportsRoute);
    }

    [RelayCommand]
    private Task NavigateTasksAsync()
    {
        var tasksRoute = string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase)
            ? "CompanyTasksPage"
            : "TasksPage";

        return NavigationService.GoToRootAsync(tasksRoute);
    }

    [RelayCommand]
    private Task NavigateMessagesAsync()
    {
        var employeesRoute = string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase)
            ? "CompanyEmployeesPage"
            : "MessagesPage";

        return NavigationService.GoToRootAsync(employeesRoute);
    }

    [RelayCommand]
    private Task NavigateNotificationsAsync()
    {
        var subscriptionsRoute = string.Equals(UserSession.Role, "company", StringComparison.OrdinalIgnoreCase)
            ? "CompanySubscriptionsPage"
            : "NotificationsPage";

        return NavigationService.GoToRootAsync(subscriptionsRoute);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await RealtimeConnectionManager.DisconnectAllAsync();
        UserSession.Clear();
        CanAccessReportsPage = false;
        await NavigationService.GoToRootAsync("MainPage");
    }
}