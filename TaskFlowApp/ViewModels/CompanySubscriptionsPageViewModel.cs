using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Models.Tenant;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanySubscriptionsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    TenantApiClient tenantApiClient,
    IdentityApiClient identityApiClient,
    ProjectManagementApiClient projectManagementApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int TasksPageSize = 100;

    public ObservableCollection<PlanUsageRow> PlanUsageRows { get; } = [];

    [ObservableProperty]
    private string activePlanName = "Bilinmiyor";

    [ObservableProperty]
    private string activePlanPriceText = "-";

    [ObservableProperty]
    private string internalReportingText = "İç raporlama durumu bilinmiyor.";

    [ObservableProperty]
    private bool isSubscriptionCancelled;

    [ObservableProperty]
    private string subscriptionActionTitle = "Abonelik İptali";

    [ObservableProperty]
    private string subscriptionActionButtonText = "Aboneliği İptal Et";

    [ObservableProperty]
    private string subscriptionActionButtonColor = "#B91C1C";

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
            StatusText = string.Empty;

            var companyId = UserSession.CompanyId.Value;

            var plansTask = tenantApiClient.GetCompanyPlansAsync();
            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var tasksTask = LoadAllCompanyTasksAsync(companyId);

            await Task.WhenAll(plansTask, usersTask, groupsTask, tasksTask);

            var plans = (await plansTask ?? [])
                .Where(plan => !string.IsNullOrWhiteSpace(plan.PlanName))
                .ToList();
            var users = await usersTask ?? [];
            var groups = await groupsTask ?? [];
            var tasks = await tasksTask;

            var usedUserCount = users
                .Where(user => user.Id != Guid.Empty)
                .Select(user => user.Id)
                .Distinct()
                .Count();
            var usedTeamCount = groups
                .Where(group => !string.IsNullOrWhiteSpace(group.GroupName))
                .Select(group => group.GroupName.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var usedIndividualTaskCount = tasks.Count(task => !IsGroupTask(task));

            var activePlan = ResolveActivePlan(plans, usedUserCount, usedTeamCount, usedIndividualTaskCount);
            BuildPlanSummary(activePlan, usedUserCount, usedTeamCount, usedIndividualTaskCount);
            StatusText = "Abonelik plan ve kullanım bilgileri güncellendi.";
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

    [RelayCommand]
    private Task CancelSubscriptionAsync()
    {
        ErrorMessage = string.Empty;

        if (!IsSubscriptionCancelled)
        {
            IsSubscriptionCancelled = true;
            StatusText = "Abonelik iptal talebiniz alındı. İşlem için yönetici onayı gereklidir.";
        }
        else
        {
            IsSubscriptionCancelled = false;
            StatusText = "Aboneliğiniz devam edecek şekilde güncellendi.";
        }

        return Task.CompletedTask;
    }

    private async Task<List<CompanyTaskDto>> LoadAllCompanyTasksAsync(Guid companyId)
    {
        var firstPage = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, 1, TasksPageSize);
        var allTasks = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;

        if (allTasks.Count >= totalCount)
        {
            return allTasks;
        }

        var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : TasksPageSize;
        var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));

        for (var page = 2; page <= totalPages; page++)
        {
            var pageResult = await projectManagementApiClient.GetAllTasksByCompanyIdAsync(companyId, page, effectivePageSize);
            var pageItems = pageResult?.Items ?? [];
            if (pageItems.Count == 0)
            {
                break;
            }

            allTasks.AddRange(pageItems);
        }

        return allTasks;
    }

    private void BuildPlanSummary(CompanyPlanDto activePlan, int usedUserCount, int usedTeamCount, int usedIndividualTaskCount)
    {
        ActivePlanName = string.IsNullOrWhiteSpace(activePlan.PlanName) ? "Bilinmiyor" : activePlan.PlanName;
        ActivePlanPriceText = activePlan.PlanPrice <= 0
            ? "Ücretsiz"
            : string.Format(CultureInfo.GetCultureInfo("tr-TR"), "{0:N0} TL / ay", activePlan.PlanPrice);
        InternalReportingText = activePlan.PlanProperties.IsInternalReportingEnabled
            ? "İç raporlama: Aktif"
            : "İç raporlama: Kapalı";

        PlanUsageRows.Clear();
        PlanUsageRows.Add(new PlanUsageRow(
            "Kullanıcı Limiti",
            activePlan.PlanProperties.PeopleAddedLimit.ToString(CultureInfo.InvariantCulture),
            usedUserCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.PeopleAddedLimit, usedUserCount)));
        PlanUsageRows.Add(new PlanUsageRow(
            "Takım Limiti",
            activePlan.PlanProperties.TeamLimit.ToString(CultureInfo.InvariantCulture),
            usedTeamCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.TeamLimit, usedTeamCount)));
        PlanUsageRows.Add(new PlanUsageRow(
            "Bireysel Görev Limiti",
            activePlan.PlanProperties.IndividualTaskLimit.ToString(CultureInfo.InvariantCulture),
            usedIndividualTaskCount.ToString(CultureInfo.InvariantCulture),
            FormatRemaining(activePlan.PlanProperties.IndividualTaskLimit, usedIndividualTaskCount)));
    }

    private static CompanyPlanDto ResolveActivePlan(
        IEnumerable<CompanyPlanDto> plans,
        int usedUserCount,
        int usedTeamCount,
        int usedIndividualTaskCount)
    {
        var orderedPlans = plans
            .OrderBy(plan => plan.PlanPrice)
            .ThenBy(plan => plan.PlanProperties.PeopleAddedLimit)
            .ThenBy(plan => plan.PlanProperties.TeamLimit)
            .ThenBy(plan => plan.PlanProperties.IndividualTaskLimit)
            .ToList();

        if (orderedPlans.Count == 0)
        {
            return new CompanyPlanDto();
        }

        foreach (var plan in orderedPlans)
        {
            if (usedUserCount <= plan.PlanProperties.PeopleAddedLimit
                && usedTeamCount <= plan.PlanProperties.TeamLimit
                && usedIndividualTaskCount <= plan.PlanProperties.IndividualTaskLimit)
            {
                return plan;
            }
        }

        return orderedPlans[^1];
    }

    private static string FormatRemaining(int limit, int usage)
    {
        var remaining = limit - usage;
        return remaining >= 0
            ? remaining.ToString(CultureInfo.InvariantCulture)
            : $"-{Math.Abs(remaining).ToString(CultureInfo.InvariantCulture)}";
    }

    private static bool IsGroupTask(CompanyTaskDto task)
    {
        var category = task.CategoryName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        return category.Contains("grup", StringComparison.OrdinalIgnoreCase)
            || category.Contains("group", StringComparison.OrdinalIgnoreCase)
            || category.Contains("team", StringComparison.OrdinalIgnoreCase);
    }

    partial void OnIsSubscriptionCancelledChanged(bool value)
    {
        SubscriptionActionTitle = value ? "Aboneliği Devam Ettir" : "Abonelik İptali";
        SubscriptionActionButtonText = value ? "Aboneliğinizi devam ettirin" : "Aboneliği İptal Et";
        SubscriptionActionButtonColor = value ? "#16A34A" : "#B91C1C";
    }
}

public sealed record PlanUsageRow(string Feature, string Limit, string Usage, string Remaining);
