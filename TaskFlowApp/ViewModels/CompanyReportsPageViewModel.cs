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
    private const int InitialPageSize = 40;
    private const int FullLoadPageSize = 100;

    public ObservableCollection<ReportDto> Reports { get; } = [];

    [ObservableProperty]
    private int totalReportCount;

    [ObservableProperty]
    private int currentMonthReportCount;

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadReportsInternalAsync(loadAllReports: false);
    }

    [RelayCommand]
    private async Task LoadAllReportsAsync()
    {
        await LoadReportsInternalAsync(loadAllReports: true);
    }

    private async Task LoadReportsInternalAsync(bool loadAllReports)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var pageSize = loadAllReports ? FullLoadPageSize : InitialPageSize;
            var firstPage = await reportApiClient.GetAllReportsAsync(1, pageSize);
            var items = firstPage?.Items?.ToList() ?? [];
            var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : items.Count;

            if (loadAllReports && items.Count < totalCount)
            {
                var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : pageSize;
                var totalPageCount = (int)Math.Ceiling(totalCount / (double)effectivePageSize);

                for (var page = 2; page <= totalPageCount; page++)
                {
                    var pageResult = await reportApiClient.GetAllReportsAsync(page, effectivePageSize);
                    var pageItems = pageResult?.Items ?? [];
                    if (pageItems.Count == 0)
                    {
                        break;
                    }

                    foreach (var report in pageItems)
                    {
                        items.Add(report);
                    }
                }
            }

            Reports.Clear();
            foreach (var report in items)
            {
                Reports.Add(report);
            }

            var now = DateTime.UtcNow;
            TotalReportCount = totalCount;
            CurrentMonthReportCount = items.Count(report =>
                report.CreatedAt.Year == now.Year &&
                report.CreatedAt.Month == now.Month);

            StatusText = loadAllReports
                ? $"Tum raporlar listeleniyor: {Reports.Count}/{TotalReportCount} | Bu ay: {CurrentMonthReportCount}"
                : $"Toplam rapor: {TotalReportCount} | Listelenen: {Reports.Count} | Bu ay: {CurrentMonthReportCount}";
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
