using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Infrastructure.Helpers;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class AllIndividualTasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int TasksPageSize = 100;

    public ObservableCollection<CompanyTaskDto> AllTasks { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (UserSession.UserId is null)
        {
            ErrorMessage = "Kullanici bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await LoadWorkerReportAccessStateAsync();

            var userId = UserSession.UserId.Value;
            var tasks = await LoadAllIndividualTasksAsync(userId);

            AllTasks.Clear();
            foreach (var task in tasks)
            {
                AllTasks.Add(task);
            }
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

    [RelayCommand]
    private Task GoBackAsync()
    {
        return NavigationService.GoToRootAsync(AppRoutes.Tasks);
    }

    private async Task<List<CompanyTaskDto>> LoadAllIndividualTasksAsync(Guid userId)
    {
        var firstPage = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(userId, 1, TasksPageSize);
        var allTasks = firstPage?.Items?.ToList() ?? [];
        var totalCount = firstPage?.TotalCount > 0 ? firstPage.TotalCount : allTasks.Count;
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

        return allTasks
            .Select(MapIndividualTask)
            .OrderBy(task => TaskStatusHelper.IsCompletedStatus(task.StatusName))
            .ThenBy(task => task.DeadlineTime)
            .ThenBy(task => task.TaskName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CompanyTaskDto MapIndividualTask(IndividualTaskDto task)
    {
        var statusName = string.IsNullOrWhiteSpace(task.StatusName) ? TaskStatusHelper.DefaultOpenStatus : task.StatusName;
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName) ? "Bireysel" : task.CategoryName;
        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName) ? "Belirtilmedi" : task.TaskPriorityName;

        return NormalizeCategoryAndPriority(new CompanyTaskDto
        {
            TaskName = task.TaskTitle,
            Description = task.Description,
            DeadlineTime = task.Deadline,
            StatusName = statusName,
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        });
    }

    private static bool IsPriorityValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().ToLowerInvariant() is "dusuk"
            or "orta"
            or "yuksek"
            or "low"
            or "medium"
            or "high";
    }

    private static CompanyTaskDto NormalizeCategoryAndPriority(CompanyTaskDto task)
    {
        var categoryName = string.IsNullOrWhiteSpace(task.CategoryName)
            ? "Bireysel"
            : task.CategoryName.Trim();

        var priorityName = string.IsNullOrWhiteSpace(task.TaskPriorityName)
            ? "Belirtilmedi"
            : task.TaskPriorityName.Trim();

        if (IsPriorityValue(categoryName))
        {
            if (!IsPriorityValue(priorityName))
            {
                priorityName = categoryName;
            }

            categoryName = "Bireysel";
        }

        return task with
        {
            CategoryName = categoryName,
            TaskPriorityName = priorityName
        };
    }

}
