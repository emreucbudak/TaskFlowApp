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

public partial class TasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ProjectManagementApiClient projectManagementApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int TasksPageSize = 100;
    private const int MaxPageTraversal = 200;
    private const int PreviewTaskCount = 4;

    private readonly List<CompanyTaskDto> allIndividualTasks = [];
    private readonly List<CompanyTaskDto> allGroupTasks = [];
    private bool isShowingAllIndividualTasks;
    private bool isShowingAllGroupTasks;

    [ObservableProperty]
    private bool canShowAllIndividualTasks;

    [ObservableProperty]
    private bool canShowAllGroupTasks;

    public ObservableCollection<CompanyTaskDto> IndividualTasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> GroupTasks { get; } = [];

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

            var userId = UserSession.UserId.Value;
            var individualTasksTask = LoadAllIndividualTasksAsync(userId);
            var groupTasksTask = LoadAllGroupTasksSafeAsync(UserSession.CompanyId);

            await Task.WhenAll(individualTasksTask, groupTasksTask);

            var individualTasks = await individualTasksTask;
            var groupTasks = await groupTasksTask;

            allIndividualTasks.Clear();
            allGroupTasks.Clear();
            allIndividualTasks.AddRange(individualTasks);
            allGroupTasks.AddRange(groupTasks);

            isShowingAllIndividualTasks = false;
            isShowingAllGroupTasks = false;
            CanShowAllIndividualTasks = allIndividualTasks.Count > PreviewTaskCount;
            CanShowAllGroupTasks = allGroupTasks.Count > PreviewTaskCount;

            RefreshVisibleTaskCollections();
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

    private async Task<List<CompanyTaskDto>> LoadAllIndividualTasksAsync(Guid userId)
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

    private async Task<List<CompanyTaskDto>> LoadAllGroupTasksSafeAsync(Guid? companyId)
    {
        if (companyId is null || companyId.Value == Guid.Empty)
        {
            return [];
        }

        try
        {
            return await LoadAllGroupTasksAsync(companyId.Value);
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

    private async Task<List<CompanyTaskDto>> LoadAllGroupTasksAsync(Guid companyId)
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

        return allTasks
            .Select(NormalizeCategoryAndPriority)
            .Select(task => task with { CategoryName = "Grup" })
            .ToList();
    }

    private static CompanyTaskDto MapIndividualTask(IndividualTaskDto task)
    {
        var statusName = string.IsNullOrWhiteSpace(task.StatusName) ? "Acik" : task.StatusName;
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