using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
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
    private const int MonthSelectionCount = 12;
    private static readonly CultureInfo TurkishCulture = new("tr-TR");

    private List<CompanyUserDto> _companyUsers = [];
    private bool _isInitializingMonthSelection;

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

    [ObservableProperty]
    private int completedTaskPercentage;

    [ObservableProperty]
    private int overdueTaskPercentage;

    [ObservableProperty]
    private int resolvedReportPercentage;

    [ObservableProperty]
    private int rejectedReportPercentage;

    [ObservableProperty]
    private string completedTaskLegendText = "0 (%0)";

    [ObservableProperty]
    private string overdueTaskLegendText = "0 (%0)";

    [ObservableProperty]
    private string resolvedReportLegendText = "0 (%0)";

    [ObservableProperty]
    private string rejectedReportLegendText = "0 (%0)";

    [ObservableProperty]
    private string bestWorkerName = "Henüz belirlenmedi";

    [ObservableProperty]
    private string bestWorkerScoreText = string.Empty;

    [ObservableProperty]
    private string worstWorkerName = "Henüz belirlenmedi";

    [ObservableProperty]
    private string worstWorkerScoreText = string.Empty;

    [ObservableProperty]
    private bool hasBestWorkerScore;

    [ObservableProperty]
    private bool hasWorstWorkerScore;

    [ObservableProperty]
    private IReadOnlyList<DashboardMonthOption> monthOptions = [];

    [ObservableProperty]
    private DashboardMonthOption? selectedMonthOption;

    [ObservableProperty]
    private string selectedMonthDisplayText = "Dönem Seçin";

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            EnsureMonthOptionsInitialized();

            var companyId = UserSession.CompanyId.Value;
            var period = DateOnly.FromDateTime(DateTime.UtcNow);

            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var tasksTask = LoadAllCompanyTasksSafeAsync(companyId);
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
            _companyUsers = users.ToList();
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
            var individualTaskCount = totalTasksFromStats > 0
                ? totalTasksFromStats
                : await LoadAllCompanyIndividualTaskCountAsync(users);

            TotalTeamCount = normalizedGroups.Count;
            TotalWorkerCount = companyUserIds.Count;
            TotalTaskCount = tasks.Count + individualTaskCount;
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

            await RefreshEmployeeRankingAsync();
            UpdateTaskDistribution();
            UpdateReportDistribution();

            StatusText = $"Ekip: {TotalTeamCount} | Çalışan: {TotalWorkerCount} | Görev: {TotalTaskCount}";
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

    private void UpdateEmployeeRanking(
        IReadOnlyCollection<CompanyUserDto> users,
        IReadOnlyCollection<Models.Stats.WorkerStatsDto> companyStats)
    {
        BestWorkerName = "Henüz belirlenmedi";
        BestWorkerScoreText = string.Empty;
        WorstWorkerName = "Henüz belirlenmedi";
        WorstWorkerScoreText = string.Empty;
        HasBestWorkerScore = false;
        HasWorstWorkerScore = false;

        if (companyStats.Count == 0)
        {
            return;
        }

        var userNameMap = users
            .Where(user => user.Id != Guid.Empty)
            .GroupBy(user => user.Id)
            .ToDictionary(
                grouped => grouped.Key,
                grouped =>
                {
                    var name = grouped.First().Name?.Trim();
                    return string.IsNullOrWhiteSpace(name) ? "Bilinmeyen Çalışan" : name;
                });

        var rankedStats = companyStats
            .OrderByDescending(item => item.TotalPoints)
            .ThenByDescending(item => item.TotalTasksCompleted)
            .ThenBy(item => ResolveUserDisplayName(item.UserId, userNameMap), StringComparer.OrdinalIgnoreCase)
            .ToList();

        var best = rankedStats[0];
        var worst = rankedStats[^1];

        BestWorkerName = ResolveUserDisplayName(best.UserId, userNameMap);
        HasBestWorkerScore = best.TotalPoints > 0;
        BestWorkerScoreText = HasBestWorkerScore ? $"{best.TotalPoints} puan" : string.Empty;

        WorstWorkerName = ResolveUserDisplayName(worst.UserId, userNameMap);
        HasWorstWorkerScore = worst.TotalPoints > 0;
        WorstWorkerScoreText = HasWorstWorkerScore ? $"{worst.TotalPoints} puan" : string.Empty;
    }

    private static string ResolveUserDisplayName(Guid userId, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        if (userId == Guid.Empty)
        {
            return "Bilinmeyen Çalışan";
        }

        return userNameMap.TryGetValue(userId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : "Bilinmeyen Çalışan";
    }

    private void UpdateTaskDistribution()
    {
        var total = CompletedTaskCount + OverdueTaskCount;
        if (total <= 0)
        {
            CompletedTaskPercentage = 0;
            OverdueTaskPercentage = 0;
            CompletedTaskLegendText = "0 (%0)";
            OverdueTaskLegendText = "0 (%0)";
            return;
        }

        CompletedTaskPercentage = (int)Math.Round(
            CompletedTaskCount * 100d / total,
            MidpointRounding.AwayFromZero);
        OverdueTaskPercentage = Math.Max(0, 100 - CompletedTaskPercentage);

        CompletedTaskLegendText = $"{CompletedTaskCount} (%{CompletedTaskPercentage})";
        OverdueTaskLegendText = $"{OverdueTaskCount} (%{OverdueTaskPercentage})";
    }

    private void UpdateReportDistribution()
    {
        var total = ResolvedReportCount + RejectedReportCount;
        if (total <= 0)
        {
            ResolvedReportPercentage = 0;
            RejectedReportPercentage = 0;
            ResolvedReportLegendText = "0 (%0)";
            RejectedReportLegendText = "0 (%0)";
            return;
        }

        ResolvedReportPercentage = (int)Math.Round(
            ResolvedReportCount * 100d / total,
            MidpointRounding.AwayFromZero);
        RejectedReportPercentage = Math.Max(0, 100 - ResolvedReportPercentage);

        ResolvedReportLegendText = $"{ResolvedReportCount} (%{ResolvedReportPercentage})";
        RejectedReportLegendText = $"{RejectedReportCount} (%{RejectedReportPercentage})";
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

    private async Task<List<Models.ProjectManagement.CompanyTaskDto>> LoadAllCompanyTasksSafeAsync(Guid companyId)
    {
        try
        {
            return await LoadAllCompanyTasksAsync(companyId);
        }
        catch (ApiException)
        {
            return [];
        }
        catch (HttpRequestException)
        {
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
    }

    private async Task<int> LoadAllCompanyIndividualTaskCountAsync(IEnumerable<CompanyUserDto> users)
    {
        var userIds = users
            .Select(user => user.Id)
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (userIds.Count == 0)
        {
            return 0;
        }

        var countJobs = userIds
            .Select(LoadIndividualTaskCountByUserIdSafeAsync)
            .ToList();
        var counts = await Task.WhenAll(countJobs);
        return counts.Sum();
    }

    private async Task<int> LoadIndividualTaskCountByUserIdSafeAsync(Guid userId)
    {
        try
        {
            var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, TasksPageSize);
            var pageItemsCount = firstPage?.Items?.Count ?? 0;
            return firstPage?.TotalCount > 0 ? firstPage.TotalCount : pageItemsCount;
        }
        catch (ApiException)
        {
            return 0;
        }
        catch (HttpRequestException)
        {
            return 0;
        }
        catch (TaskCanceledException)
        {
            return 0;
        }
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
    partial void OnSelectedMonthOptionChanged(DashboardMonthOption? value)
    {
        SelectedMonthDisplayText = value?.Label ?? "Dönem Seçin";

        if (_isInitializingMonthSelection || value is null)
        {
            return;
        }

        _ = RefreshEmployeeRankingAsync();
    }

    private async Task RefreshEmployeeRankingAsync()
    {
        if (UserSession.CompanyId is null)
        {
            return;
        }

        if (_companyUsers.Count == 0)
        {
            UpdateEmployeeRanking([], []);
            return;
        }

        var selectedPeriod = SelectedMonthOption?.Period ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var companyUserIds = _companyUsers
            .Select(user => user.Id)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        try
        {
            var stats = await LoadAllStatsByPeriodAsync(selectedPeriod);
            var companyStats = stats
                .Where(workerStat => companyUserIds.Contains(workerStat.UserId))
                .ToList();
            UpdateEmployeeRanking(_companyUsers, companyStats);
        }
        catch
        {
            UpdateEmployeeRanking(_companyUsers, []);
        }
    }

    private void EnsureMonthOptionsInitialized()
    {
        if (MonthOptions.Count > 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var options = Enumerable
            .Range(0, MonthSelectionCount)
            .Select(offset =>
            {
                var date = now.AddMonths(-offset);
                var monthName = TurkishCulture.DateTimeFormat.GetMonthName(date.Month);
                var label = $"{char.ToUpper(monthName[0], TurkishCulture)}{monthName[1..]} {date.Year}";
                return new DashboardMonthOption(label, new DateOnly(date.Year, date.Month, 1));
            })
            .ToList();

        _isInitializingMonthSelection = true;
        MonthOptions = options;
        SelectedMonthOption = options[0];
        _isInitializingMonthSelection = false;
    }

    public sealed record DashboardMonthOption(string Label, DateOnly Period);
}

