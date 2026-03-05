using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Stats;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class DashBoardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    StatsApiClient statsApiClient,
    ProjectManagementApiClient projectManagementApiClient,
    ChatApiClient chatApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    [ObservableProperty]
    private int totalAssigned;

    [ObservableProperty]
    private int totalCompleted;

    [ObservableProperty]
    private int overdueTasks;

    [ObservableProperty]
    private int individualTaskCount;

    [ObservableProperty]
    private int groupTaskCount;

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
            ErrorMessage = "Oturum bilgisi eksik. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var userId = UserSession.UserId.Value;
            var period = DateOnly.FromDateTime(DateTime.UtcNow);

            var tasksTask = projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, 10);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);

            WorkerStatsDto? stats = null;
            try
            {
                stats = await statsApiClient.GetWorkerStatsByUserAndPeriodAsync(userId, period);
            }
            catch (ApiException ex) when (ex.StatusCode == 404)
            {
                // Bu ay icin istatistik kaydi olmayabilir; dashboard yine de acilmalidir.
                stats = null;
            }

            await Task.WhenAll(tasksTask, unreadTask);

            var tasks = await tasksTask;
            var unread = await unreadTask;
            var individualTasks = tasks?.TotalCount > 0
                ? tasks.TotalCount
                : tasks?.Items.Count ?? 0;
            var assignedTasks = stats?.TotalTasksAssigned ?? individualTasks;
            var groupedTasks = Math.Max(0, assignedTasks - individualTasks);

            TotalAssigned = assignedTasks;
            TotalCompleted = stats?.TotalTasksCompleted ?? 0;
            OverdueTasks = stats?.OverdueIncompleteTasksCount ?? 0;
            IndividualTaskCount = individualTasks;
            GroupTaskCount = groupedTasks;
            UnreadMessageCount = unread;

            StatusText = string.Empty;
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
            ErrorMessage = "Bir sorun oluştu. Lütfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
