using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Report;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class CompanyReportsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ReportApiClient reportApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int FixedPageSize = 5;

    public ObservableCollection<CompanyReportCardItem> Reports { get; } = [];

    [ObservableProperty]
    private int totalReportCount;

    [ObservableProperty]
    private int currentMonthReportCount;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int totalPageCount = 1;

    [ObservableProperty]
    private bool canGoPrevious;

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private string pageInfoText = "Sayfa 1";

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
            var pageResult = await reportApiClient.GetAllReportsAsync(safePage, FixedPageSize);
            var items = pageResult?.Items?.ToList() ?? [];
            var totalCount = pageResult?.TotalCount > 0 ? pageResult.TotalCount : items.Count;
            var resolvedTotalPageCount = totalCount > 0
                ? (int)Math.Ceiling(totalCount / (double)FixedPageSize)
                : 1;
            var resolvedPage = pageResult?.Page > 0
                ? Math.Min(pageResult.Page, resolvedTotalPageCount)
                : Math.Min(safePage, resolvedTotalPageCount);

            Reports.Clear();
            foreach (var report in items)
            {
                Reports.Add(MapReport(report));
            }

            var now = DateTime.UtcNow;
            TotalReportCount = totalCount;
            CurrentMonthReportCount = items.Count(report =>
                report.CreatedAt.Year == now.Year &&
                report.CreatedAt.Month == now.Month);

            CurrentPage = resolvedPage;
            TotalPageCount = Math.Max(1, resolvedTotalPageCount);
            CanGoPrevious = CurrentPage > 1;
            CanGoNext = CurrentPage < TotalPageCount;
            PageInfoText = $"Sayfa {CurrentPage}";
            StatusText = $"Toplam rapor: {TotalReportCount} | Gösterilen: {Reports.Count}";
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

    private static CompanyReportCardItem MapReport(ReportDto report)
    {
        return new CompanyReportCardItem
        {
            Title = report.Title,
            Description = report.Description,
            ReportTopicName = ResolveTopicName(report.ReportTopicId),
            ReportStatusName = ResolveStatusName(report.ReportStatusId),
            CreatedAt = report.CreatedAt
        };
    }

    private static string ResolveTopicName(int reportTopicId)
    {
        return reportTopicId switch
        {
            ReportTopics.BugReportId => ReportTopics.BugReport,
            ReportTopics.FeedbackId => ReportTopics.Feedback,
            ReportTopics.OtherId => ReportTopics.Other,
            _ => $"Konu #{reportTopicId}"
        };
    }

    private static string ResolveStatusName(int reportStatusId)
    {
        return reportStatusId switch
        {
            1 => "Bildirildi",
            2 => "İşleme Alındı",
            3 => "Çözüldü",
            4 => "Reddedildi",
            _ => $"Durum #{reportStatusId}"
        };
    }
}

public sealed record CompanyReportCardItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ReportTopicName { get; init; } = string.Empty;
    public string ReportStatusName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
