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
    ProjectManagementApiClient projectManagementApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int TasksPageSize = 100;
    private const int MaxPageTraversal = 200;

    public ObservableCollection<CompanyTaskDto> Tasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> IndividualTasks { get; } = [];
    public ObservableCollection<CompanyTaskDto> GroupTasks { get; } = [];

    [ObservableProperty]
    private int totalTaskCount;

    [ObservableProperty]
    private int openTaskCount;

    [ObservableProperty]
    private int overdueTaskCount;

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

            var items = await LoadAllCompanyTasksAsync(UserSession.CompanyId.Value);

            Tasks.Clear();
            IndividualTasks.Clear();
            GroupTasks.Clear();
            foreach (var item in items)
            {
                Tasks.Add(item);
                if (IsGroupTask(item))
                {
                    GroupTasks.Add(item);
                }
                else
                {
                    IndividualTasks.Add(item);
                }
            }

            var now = DateOnly.FromDateTime(DateTime.UtcNow);
            TotalTaskCount = items.Count;
            OpenTaskCount = items.Count(item => !IsCompletedStatus(item.StatusName));
            OverdueTaskCount = items.Count(item => item.DeadlineTime < now && !IsCompletedStatus(item.StatusName));

            StatusText = $"Toplam: {TotalTaskCount} | Acik: {OpenTaskCount} | Geciken: {OverdueTaskCount}";
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
}
