using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;

namespace TaskFlowApp.ViewModels;

public abstract partial class PageViewModelBase(INavigationService navigationService, IUserSession userSession) : ObservableObject
{
    protected IUserSession UserSession { get; } = userSession;
    protected INavigationService NavigationService { get; } = navigationService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [RelayCommand]
    private Task NavigateHomeAsync() => NavigationService.GoToRootAsync("DashBoardPage");

    [RelayCommand]
    private Task NavigateReportsAsync() => NavigationService.GoToRootAsync("ReportsPage");

    [RelayCommand]
    private Task NavigateTasksAsync() => NavigationService.GoToRootAsync("TasksPage");

    [RelayCommand]
    private Task NavigateMessagesAsync() => NavigationService.GoToRootAsync("MessagesPage");

    [RelayCommand]
    private Task NavigateNotificationsAsync() => NavigationService.GoToRootAsync("NotificationsPage");

    [RelayCommand]
    private Task LogoutAsync()
    {
        UserSession.Clear();
        return NavigationService.GoToRootAsync("MainPage");
    }
}
