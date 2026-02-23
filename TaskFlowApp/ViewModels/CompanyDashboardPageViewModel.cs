using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanyDashboardPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    StatsApiClient statsApiClient,
    ProjectManagementApiClient projectManagementApiClient,
    ReportApiClient reportApiClient,
    IdentityApiClient identityApiClient,
    TenantApiClient tenantApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int TasksPageSize = 100;
    private const int ReportsPageSize = 100;
    private const int StatsPageSize = 100;
    private const int MaxPageTraversal = 200;

    [ObservableProperty]
    private int totalTeamCount;

    [ObservableProperty]
    private int totalWorkerCount;

    [ObservableProperty]
    private int totalTaskCount;

    [ObservableProperty]
    private int totalReportCount;

    [ObservableProperty]
    private int completedTaskCount;

    [ObservableProperty]
    private int overdueTaskCount;

    [ObservableProperty]
    private int resolvedReportCount;

    [ObservableProperty]
    private int rejectedReportCount;

    [ObservableProperty]
    private int availablePlanCount;

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

            var companyId = UserSession.CompanyId.Value;
            var period = DateOnly.FromDateTime(DateTime.UtcNow);

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var tasksTask = LoadAllCompanyTasksAsync(companyId);
            var reportsTask = LoadAllReportsAsync();
            var statsTask = LoadAllStatsByPeriodAsync(period);
            var plansTask = tenantApiClient.GetCompanyPlansAsync();

            await Task.WhenAll(usersTask, groupsTask, tasksTask, reportsTask, statsTask, plansTask);

            var users = await usersTask ?? [];
            var normalizedGroups = NormalizeGroups(await groupsTask ?? []);
            var tasks = await tasksTask;
            var reports = await reportsTask;
            var stats = await statsTask;
            var plans = await plansTask ?? [];
            var companyUserIds = users
                .Select(user => user.Id)
                .Where(id => id != Guid.Empty)
                .ToHashSet();

            var companyReports = reports
                .Where(report => companyUserIds.Contains(report.ReportingUserId))
                .ToList();
            var companyStats = stats
                .Where(workerStat => companyUserIds.Contains(workerStat.UserId))
                .ToList();
            var overdueFromTasks = tasks.Count(task =>
                task.DeadlineTime < period &&
                !IsCompletedStatus(task.StatusName));
            var completedFromTasks = tasks.Count(task => IsCompletedStatus(task.StatusName));
            var totalTasksFromStats = companyStats.Sum(item => item.TotalTasksAssigned);

            TotalTeamCount = normalizedGroups.Count;
            TotalWorkerCount = companyUserIds.Count;
            TotalTaskCount = totalTasksFromStats > 0 ? totalTasksFromStats : tasks.Count;
            TotalReportCount = companyReports.Count;
            CompletedTaskCount = companyStats.Count > 0
                ? companyStats.Sum(item => item.TotalTasksCompleted)
                : completedFromTasks;
            OverdueTaskCount = companyStats.Count > 0
                ? companyStats.Sum(item => item.OverdueIncompleteTasksCount)
                : overdueFromTasks;
            ResolvedReportCount = companyReports.Count(item => IsResolvedReportStatus(item.ReportStatusId));
            RejectedReportCount = companyReports.Count(item => IsRejectedReportStatus(item.ReportStatusId));
            AvailablePlanCount = plans.Count;

            StatusText = $"Ekip: {TotalTeamCount} | Calisan: {TotalWorkerCount} | Gorev: {TotalTaskCount}";
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

    private static bool IsResolvedReportStatus(int reportStatusId) => reportStatusId == 3;

    private static bool IsRejectedReportStatus(int reportStatusId) => reportStatusId == 4;

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

    private async Task<List<Models.ProjectManagement.CompanyTaskDto>> LoadAllCompanyTasksAsync(Guid companyId)
    {
        var allTasks = new List<Models.ProjectManagement.CompanyTaskDto>();
        var pageNumber = 1;

        while (pageNumber <= MaxPageTraversal)
        {
            var response = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, pageNumber, TasksPageSize);
            var pageItems = response?.Items ?? [];

            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);

            if (pageItems.Count < TasksPageSize)
            {
                break;
            }

            pageNumber++;
        }

        return allTasks;
    }

    private async Task<List<Models.Report.ReportDto>> LoadAllReportsAsync()
    {
        var firstPage = await reportApiClient.GetAllReportsAsync(1, ReportsPageSize);
        var allReports = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allReports.Count;

        if (allReports.Count >= totalCount)
        {
            return allReports;
        }

        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : ReportsPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));
        totalPages = Math.Min(totalPages, MaxPageTraversal);

        for (var page = 2; page <= totalPages; page++)
        {
            var response = await reportApiClient.GetAllReportsAsync(page, effectivePageSize);
            var pageItems = response?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allReports.AddRange(pageItems);
        }

        return allReports;
    }

    private async Task<List<Models.Stats.WorkerStatsDto>> LoadAllStatsByPeriodAsync(DateOnly period)
    {
        var firstPage = await statsApiClient.GetAllWorkersStatsByPeriodQueryRequestAsync(new
        {
            Period = period,
            Page = 1,
            PageSize = StatsPageSize
        });
        var allStats = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allStats.Count;

        if (allStats.Count >= totalCount)
        {
            return allStats;
        }

        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : StatsPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));
        totalPages = Math.Min(totalPages, MaxPageTraversal);

        for (var page = 2; page <= totalPages; page++)
        {
            var response = await statsApiClient.GetAllWorkersStatsByPeriodQueryRequestAsync(new
            {
                Period = period,
                Page = page,
                PageSize = effectivePageSize
            });
            var pageItems = response?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allStats.AddRange(pageItems);
        }

        return allStats;
    }

    private static List<CompanyGroupDto> NormalizeGroups(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupName))
            .GroupBy(group => group.GroupName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grouped => new CompanyGroupDto
            {
                GroupName = grouped.First().GroupName,
                WorkerName = grouped
                    .SelectMany(item => item.WorkerName ?? [])
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList(),
                DepartmenName = grouped
                    .SelectMany(item => item.DepartmenName ?? [])
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => name)
                    .ToList()
            })
            .OrderBy(group => group.GroupName)
            .ToList();
    }
}
