using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class DashBoardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    StatsApiClient statsApiClient,
    ProjectManagementApiClient projectManagementApiClient,
    ChatApiClient chatApiClient) : PageViewModelBase(navigationService, userSession)
{
    [ObservableProperty]
    private int totalAssigned;

    [ObservableProperty]
    private int totalCompleted;

    [ObservableProperty]
    private int overdueTasks;

    [ObservableProperty]
    private int myTaskCount;

    [ObservableProperty]
    private int unreadMessageCount;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is null)
        {
            ErrorMessage = "Oturum bilgisi eksik. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var userId = UserSession.UserId.Value;
            var period = DateOnly.FromDateTime(DateTime.UtcNow);

            var statsTask = statsApiClient.GetWorkerStatsByUserAndPeriodAsync(userId, period);
            var tasksTask = projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, 10);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);

            await Task.WhenAll(statsTask, tasksTask, unreadTask);

            var stats = await statsTask;
            var tasks = await tasksTask;
            var unread = await unreadTask;

            TotalAssigned = stats?.TotalTasksAssigned ?? 0;
            TotalCompleted = stats?.TotalTasksCompleted ?? 0;
            OverdueTasks = stats?.OverdueIncompleteTasksCount ?? 0;
            MyTaskCount = tasks?.Items.Count ?? 0;
            UnreadMessageCount = unread;

            StatusText = $"Atanan: {TotalAssigned} | Tamamlanan: {TotalCompleted} | Geciken: {OverdueTasks}";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Dashboard verisi alinamadi ({ex.StatusCode}).";
        }
        catch (Exception)
        {
            ErrorMessage = "Dashboard yuklenirken hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
