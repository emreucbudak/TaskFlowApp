using TaskFlowApp.Infrastructure;
using TaskFlowApp.ViewModels;

namespace TaskFlowApp.Pages;

public partial class CompanyReportsPage : ContentPage
{
    private CompanyReportsPageViewModel ViewModel => (CompanyReportsPageViewModel)BindingContext;

    public CompanyReportsPage()
    {
        InitializeComponent();
        BindingContext = ServiceLocator.GetRequiredService<CompanyReportsPageViewModel>();
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateHomeCommand.ExecuteAsync(null);
    private async void OnReportsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateReportsCommand.ExecuteAsync(null);
    private async void OnTasksTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateTasksCommand.ExecuteAsync(null);
    private async void OnMessagesTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateMessagesCommand.ExecuteAsync(null);
    private async void OnNotificationsTapped(object? sender, TappedEventArgs e) => await ViewModel.NavigateNotificationsCommand.ExecuteAsync(null);
    private async void OnLogoutTapped(object? sender, TappedEventArgs e) => await ViewModel.LogoutCommand.ExecuteAsync(null);
}
