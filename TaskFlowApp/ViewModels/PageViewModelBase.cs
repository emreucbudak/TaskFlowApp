using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public abstract partial class PageViewModelBase(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager) : ObservableObject
{
    protected IUserSession UserSession { get; } = userSession;
    protected INavigationService NavigationService { get; } = navigationService;
    protected IRealtimeConnectionManager RealtimeConnectionManager { get; } = realtimeConnectionManager;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    protected const string GenericConnectionErrorMessage = "Su anda islem gerceklestirilemiyor. Lutfen tekrar deneyin.";
    protected const string GenericLoadErrorMessage = "Veriler su anda yuklenemiyor. Lutfen tekrar deneyin.";
    protected const string SessionExpiredMessage = "Oturumunuz sona erdi. Lutfen yeniden giris yapin.";

    protected static string ResolveApiErrorMessage(ApiException exception, string defaultMessage)
    {
        return exception.StatusCode is 401 or 403
            ? SessionExpiredMessage
            : defaultMessage;
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
    private Task NavigateReportsAsync() => NavigationService.GoToRootAsync("ReportsPage");

    [RelayCommand]
    private Task NavigateTasksAsync() => NavigationService.GoToRootAsync("TasksPage");

    [RelayCommand]
    private Task NavigateMessagesAsync() => NavigationService.GoToRootAsync("MessagesPage");

    [RelayCommand]
    private Task NavigateNotificationsAsync() => NavigationService.GoToRootAsync("NotificationsPage");

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await RealtimeConnectionManager.DisconnectAllAsync();
        UserSession.Clear();
        await NavigationService.GoToRootAsync("MainPage");
    }
}
