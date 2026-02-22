using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanyTasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<CompanyTaskDto> Tasks { get; } = [];

    [ObservableProperty]
    private int totalTaskCount;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private int overdueTaskCount;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var response = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(UserSession.CompanyId.Value, 1, 50);
            var items = response?.Items ?? [];

            Tasks.Clear();
            foreach (var item in items)
            {
                Tasks.Add(item);
            }

            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            TotalTaskCount = response?.TotalCount > 0 ? response.TotalCount : items.Count;
            OpenTaskCount = items.Count(item => !IsCompletedStatus(item.StatusName));
            OverdueTaskCount = items.Count(item => item.DeadlineTime < now && !IsCompletedStatus(item.StatusName));

            StatusText = $"Toplam: {TotalTaskCount} | Acik: {OpenTaskCount} | Geciken: {OverdueTaskCount}";
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

    private static bool IsCompletedStatus(string? statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            return false;
        }

        var normalizedStatus = statusName.Trim().ToLowerInvariant();
        return normalizedStatus.Contains("tamam")
            || normalizedStatus.Contains("complete")
            || normalizedStatus.Contains("done")
            || normalizedStatus.Contains("closed");
    }
}
