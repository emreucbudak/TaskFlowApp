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

            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var tasksTask = projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, 1, 30);
            var reportsTask = reportApiClient.GetAllReportsAsync(1, 30);
            var statsTask = statsApiClient.GetAllWorkersStatsByPeriodQueryRequestAsync(new { Period = period, Page = 1, PageSize = 100 });
            var plansTask = tenantApiClient.GetCompanyPlansAsync();

            await Task.WhenAll(groupsTask, tasksTask, reportsTask, statsTask, plansTask);

            var normalizedGroups = NormalizeGroups(await groupsTask ?? []);
            var tasks = await tasksTask;
            var reports = await reportsTask;
            var stats = await statsTask;
            var plans = await plansTask ?? [];

            TotalTeamCount = normalizedGroups.Count;
            TotalWorkerCount = normalizedGroups
                .SelectMany(group => group.WorkerName ?? [])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            TotalTaskCount = tasks?.TotalCount > 0 ? tasks.TotalCount : tasks?.Items.Count ?? 0;
            TotalReportCount = reports?.TotalCount > 0 ? reports.TotalCount : reports?.Items.Count ?? 0;
            CompletedTaskCount = stats?.Items.Sum(item => item.TotalTasksCompleted) ?? 0;
            OverdueTaskCount = stats?.Items.Sum(item => item.OverdueIncompleteTasksCount) ?? 0;
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
