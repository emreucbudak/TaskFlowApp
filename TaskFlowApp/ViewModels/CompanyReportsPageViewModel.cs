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
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    public ObservableCollection<ReportDto> Reports { get; } = [];

    [ObservableProperty]
    private int totalReportCount;

    [ObservableProperty]
    private int currentMonthReportCount;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int pageSize = DefaultPageSize;

    [ObservableProperty]
    private int totalPageCount = 1;

    [ObservableProperty]
    private bool canGoPrevious;

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private string pageInfoText = "Sayfa 1 / 1";

    [RelayCommand]
    private async Task LoadAsync()
    {
        await LoadReportsInternalAsync();
    }

    [RelayCommand]
    private async Task LoadAllReportsAsync()
    {
        await LoadReportsInternalAsync();
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (IsBusy || !CanGoPrevious)
        {
            return;
        }

        CurrentPage = Math.Max(1, CurrentPage - 1);
        await LoadReportsInternalAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (IsBusy || !CanGoNext)
        {
            return;
        }

        CurrentPage += 1;
        await LoadReportsInternalAsync();
    }

    [RelayCommand]
    private async Task SetPageSizeAsync(string? requestedPageSizeText)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(requestedPageSizeText))
        {
            return;
        }

        if (!int.TryParse(requestedPageSizeText, out var requestedPageSize) || requestedPageSize <= 0)
        {
            return;
        }

        PageSize = Math.Min(MaxPageSize, requestedPageSize);
        CurrentPage = 1;
        await LoadReportsInternalAsync();
    }

    private async Task LoadReportsInternalAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            var safePage = Math.Max(1, CurrentPage);
            var safePageSize = Math.Clamp(PageSize, 1, MaxPageSize);
            var pageResult = await reportApiClient.GetAllReportsAsync(safePage, safePageSize);
            var items = pageResult?.Items?.ToList() ?? [];
            var totalCount = pageResult?.TotalCount > 0 ? pageResult.TotalCount : items.Count;
            var effectivePageSize = pageResult?.PageSize > 0 ? pageResult.PageSize : safePageSize;
            var resolvedTotalPageCount = totalCount > 0
                ? (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize))
                : 1;
            var resolvedPage = pageResult?.Page > 0
                ? Math.Min(pageResult.Page, resolvedTotalPageCount)
                : Math.Min(safePage, resolvedTotalPageCount);

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

            CurrentPage = resolvedPage;
            PageSize = effectivePageSize;
            TotalPageCount = Math.Max(1, resolvedTotalPageCount);
            CanGoPrevious = CurrentPage > 1;
            CanGoNext = CurrentPage < TotalPageCount;
            PageInfoText = $"Sayfa {CurrentPage} / {TotalPageCount}";
            StatusText = $"Toplam rapor: {TotalReportCount} | Gosterilen: {Reports.Count} | Sayfa boyutu: {PageSize}";
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
