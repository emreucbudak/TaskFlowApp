using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanyTasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    IdentityApiClient identityApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int TasksPageSize = 100;
    private const int MaxPageTraversal = 200;
    private const int PreviewTaskCount = 4;

    private readonly List<CompanyTaskDto> allIndividualTasks = [];
    private readonly List<CompanyTaskDto> allGroupTasks = [];
    private bool isShowingAllIndividualTasks;
    private bool isShowingAllGroupTasks;

    public ObservableCollection<CompanyTaskDto> Tasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> IndividualTasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> GroupTasks { get; } = [];

    [ObservableProperty]
    private int totalTaskCount;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private int overdueTaskCount;

    [ObservableProperty]
    private bool canShowAllIndividualTasks;

    [ObservableProperty]
    private bool canShowAllGroupTasks;

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

            var companyId = UserSession.CompanyId.Value;
            var groupedTasksTask = LoadAllCompanyTasksAsync(companyId);
            var individualTasksTask = LoadAllCompanyIndividualTasksAsync(companyId);

            await Task.WhenAll(groupedTasksTask, individualTasksTask);

            var items = (await groupedTasksTask)
                .Concat(await individualTasksTask)
                .ToList();

            Tasks.Clear();
            allIndividualTasks.Clear();
            allGroupTasks.Clear();
            foreach (var item in items)
            {
                Tasks.Add(item);
                if (IsGroupTask(item))
                {
                    allGroupTasks.Add(item);
                }
                else
                {
                    allIndividualTasks.Add(item);
                }
            }

            isShowingAllIndividualTasks = false;
            isShowingAllGroupTasks = false;
            CanShowAllIndividualTasks = allIndividualTasks.Count > PreviewTaskCount;
            CanShowAllGroupTasks = allGroupTasks.Count > PreviewTaskCount;
            RefreshVisibleTaskCollections();

            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            TotalTaskCount = items.Count;
            OpenTaskCount = items.Count(item => !IsCompletedStatus(item.StatusName));
            OverdueTaskCount = items.Count(item => item.DeadlineTime < now && !IsCompletedStatus(item.StatusName));

            StatusText = $"Toplam: {TotalTaskCount} | Açık: {OpenTaskCount} | Geciken: {OverdueTaskCount}";
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
    private Task ShowAllIndividualTasksAsync()
    {
        if (!CanShowAllIndividualTasks)
        {
            return Task.CompletedTask;
        }

        isShowingAllIndividualTasks = true;
        CanShowAllIndividualTasks = false;
        RefreshVisibleTaskCollections();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ShowAllGroupTasksAsync()
    {
        if (!CanShowAllGroupTasks)
        {
            return Task.CompletedTask;
        }

        isShowingAllGroupTasks = true;
        CanShowAllGroupTasks = false;
        RefreshVisibleTaskCollections();
        return Task.CompletedTask;
    }

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

    private static bool IsGroupTask(CompanyTaskDto task)
    {
        if (task is null)
        {
            return false;
        }

        var category = task.CategoryName?.Trim() ?? string.Empty;
        var taskName = task.TaskName?.Trim() ?? string.Empty;
        var description = task.Description?.Trim() ?? string.Empty;

        return category.Contains("grup", StringComparison.OrdinalIgnoreCase)
            || category.Contains("group", StringComparison.OrdinalIgnoreCase)
            || category.Contains("team", StringComparison.OrdinalIgnoreCase)
            || category.Contains("ekip", StringComparison.OrdinalIgnoreCase)
            || taskName.Contains("grup", StringComparison.OrdinalIgnoreCase)
            || taskName.Contains("group", StringComparison.OrdinalIgnoreCase)
            || taskName.Contains("team", StringComparison.OrdinalIgnoreCase)
            || taskName.Contains("ekip", StringComparison.OrdinalIgnoreCase)
            || description.Contains("grup", StringComparison.OrdinalIgnoreCase)
            || description.Contains("group", StringComparison.OrdinalIgnoreCase)
            || description.Contains("team", StringComparison.OrdinalIgnoreCase)
            || description.Contains("ekip", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<CompanyTaskDto>> LoadAllCompanyTasksAsync(Guid companyId)
    {
        var allTasks = new List<CompanyTaskDto>();
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

    private async Task<List<CompanyTaskDto>> LoadAllCompanyIndividualTasksAsync(Guid companyId)
    {
        var users = await identityApiClient.GetAllCompanyUsersAsync(companyId) ?? [];
        if (users.Count == 0)
        {
            return [];
        }

        var loadTasksByUserJobs = users
            .Where(user => user.Id != Guid.Empty)
            .Select(user => LoadIndividualTasksByUserIdSafeAsync(user.Id))
            .ToList();

        var tasksByUser = await Task.WhenAll(loadTasksByUserJobs);
        return tasksByUser
            .SelectMany(tasks => tasks)
            .GroupBy(
                task => $"{task.TaskName}|{task.Description}|{task.DeadlineTime}|{task.StatusName}|{task.CategoryName}|{task.TaskPriorityName}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private async Task<List<CompanyTaskDto>> LoadIndividualTasksByUserIdSafeAsync(Guid userId)
    {
        try
        {
            var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, TasksPageSize);
            var allTasks = firstPage?.Items?.ToList() ?? [];
            var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;

            if (allTasks.Count < totalCount)
            {
                var effectivePageSize = firstPage?.PageSize > 0 ? firstPage.PageSize : TasksPageSize;
                var totalPages = (int)Math.Ceiling(totalCount / (double)Math.Max(1, effectivePageSize));

                for (var page = 2; page <= totalPages; page++)
                {
                    var pageResult = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, page, effectivePageSize);
                    var pageItems = pageResult?.Items ?? [];
                    if (pageItems.Count == 0)
                    {
                        break;
                    }

                    allTasks.AddRange(pageItems);
                }
            }

            return allTasks.Select(MapIndividualTask).ToList();
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

    private static CompanyTaskDto MapIndividualTask(IndividualTaskDto task)
    {
        var statusName = string.IsNullOrWhiteSpace(task.StatusName) ? "Açık" : task.StatusName;
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName) ? "Bireysel" : task.CategoryName;
        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName) ? "Belirtilmedi" : task.TaskPriorityName;

        return new CompanyTaskDto
        {
            TaskName = task.TaskTitle,
            Description = task.Description,
            DeadlineTime = task.Deadline,
            StatusName = statusName,
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        };
    }

    private void RefreshVisibleTaskCollections()
    {
        IndividualTasks.Clear();
        GroupTasks.Clear();

        var individualSource = isShowingAllIndividualTasks
            ? allIndividualTasks
            : allIndividualTasks.Take(PreviewTaskCount);

        var groupSource = isShowingAllGroupTasks
            ? allGroupTasks
            : allGroupTasks.Take(PreviewTaskCount);

        foreach (var task in individualSource)
        {
            IndividualTasks.Add(task);
        }

        foreach (var task in groupSource)
        {
            GroupTasks.Add(task);
        }
    }
}
