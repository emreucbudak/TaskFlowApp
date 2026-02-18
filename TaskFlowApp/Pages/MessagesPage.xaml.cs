using System.Threading.Tasks;

namespace TaskFlowApp.Pages;

public partial class MessagesPage : ContentPage
{
    public MessagesPage()
    {
        InitializeComponent();
    }

    private async void OnHomeTapped(object sender, TappedEventArgs e) => await NavigateAsync("DashBoardPage");
    private async void OnReportsTapped(object sender, TappedEventArgs e) => await NavigateAsync("ReportsPage");
    private async void OnTasksTapped(object sender, TappedEventArgs e) => await NavigateAsync("TasksPage");
    private async void OnMessagesTapped(object sender, TappedEventArgs e) => await NavigateAsync("MessagesPage");
    private async void OnNotificationsTapped(object sender, TappedEventArgs e) => await NavigateAsync("NotificationsPage");
    private async void OnLogoutTapped(object sender, TappedEventArgs e) => await NavigateAsync("MainPage");

    private static Task NavigateAsync(string route) => Shell.Current.GoToAsync($"///{route}");
}