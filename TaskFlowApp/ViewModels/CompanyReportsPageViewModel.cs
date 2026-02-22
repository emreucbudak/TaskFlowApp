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

public partial class CompanyReportsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ReportApiClient reportApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<ReportDto> Reports { get; } = [];

    [ObservableProperty]
    private int totalReportCount;

    [ObservableProperty]
    private int currentMonthReportCount;

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

            var response = await reportApiClient.GetAllReportsAsync(1, 40);
            var items = response?.Items ?? [];

            Reports.Clear();
            foreach (var report in items)
            {
                Reports.Add(report);
            }

            var now = DateTime.UtcNow;
            TotalReportCount = response?.TotalCount > 0 ? response.TotalCount : items.Count;
            CurrentMonthReportCount = items.Count(report =>
                report.CreatedAt.Year == now.Year &&
                report.CreatedAt.Month == now.Month);

            StatusText = $"Toplam rapor: {TotalReportCount} | Bu ay: {CurrentMonthReportCount}";
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
}
