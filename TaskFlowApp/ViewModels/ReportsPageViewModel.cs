using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Report;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class ReportsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    ReportApiClient reportApiClient) : PageViewModelBase(navigationService, userSession)
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

            StatusText = $"Toplam rapor: {response?.TotalCount ?? 0}";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Raporlar alinamadi ({ex.StatusCode}).";
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
