using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Report;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class ReportsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ReportApiClient reportApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<ReportDto> Reports { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var response = await reportApiClient.GetAllReportsAsync(1, 20);

            Reports.Clear();
            foreach (var report in response?.Items ?? [])
            {
                Reports.Add(report);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Raporlar alinamadi ({ex.StatusCode}).";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "API baglantisi kurulamadi. API servisinin calistigini kontrol edin.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "API yanit vermedi. Daha sonra tekrar deneyin.";
        }
        catch (Exception)
        {
            ErrorMessage = "Raporlar yuklenirken hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
