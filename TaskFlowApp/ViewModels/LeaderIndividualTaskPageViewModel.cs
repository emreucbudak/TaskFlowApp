using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class LeaderIndividualTaskPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient,
    ProjectManagementApiClient projectManagementApiClient) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const string AccessDeniedMessageText = "Bu sayfa sadece departman liderleri icindir.";

    private readonly List<CompanyUserDto> allCompanyUsers = [];
    private Guid? managedDepartmentId;

    public ObservableCollection<AssignableDepartmentUserOption> EligibleUsers { get; } = [];
    public ObservableCollection<TaskPriorityOption> PriorityOptions { get; } = [];

    [ObservableProperty]
    private bool hasLeaderAccess;

    [ObservableProperty]
    private string accessMessage = AccessDeniedMessageText;

    [ObservableProperty]
    private AssignableDepartmentUserOption? selectedAssignedUser;

    [ObservableProperty]
    private TaskPriorityOption? selectedPriority;

    [ObservableProperty]
    private string taskTitle = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private DateTime deadlineDate = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private DateTime minimumDeadlineDate = DateTime.Today;

    [ObservableProperty]
    private bool isCreatingTask;

    [ObservableProperty]
    private string createStatus = string.Empty;

    [ObservableProperty]
    private string currentDepartmentName = string.Empty;

    public bool HasNoLeaderAccess => !HasLeaderAccess;

    partial void OnHasLeaderAccessChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoLeaderAccess));
    }

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

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            CreateStatus = string.Empty;
            MinimumDeadlineDate = DateTime.Today;
            if (DeadlineDate < MinimumDeadlineDate)
            {
                DeadlineDate = MinimumDeadlineDate;
            }

            var accessState = await LoadWorkerReportAccessStateAsync();
            if (!accessState.CanAccessReportsPage)
            {
                ApplyAccessDeniedState();
                return;
            }

            var companyUsers = await identityApiClient.GetAllCompanyUsersAsync(UserSession.CompanyId.Value) ?? [];
            allCompanyUsers.Clear();
            allCompanyUsers.AddRange(companyUsers.Where(user => user.Id != Guid.Empty));

            ConfigureManagedDepartment(accessState);

            if (!HasLeaderAccess)
            {
                ApplyAccessDeniedState();
                return;
            }

            EnsurePriorityOptions();
            StatusText = string.IsNullOrWhiteSpace(CurrentDepartmentName)
                ? "Departman lideri gorev olusturma ekrani hazir."
                : $"{CurrentDepartmentName} departmani icin bireysel gorev olusturabilirsiniz.";
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
    private async Task CreateTaskAsync()
    {
        if (IsCreatingTask || !HasLeaderAccess)
        {
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        if (managedDepartmentId is null || managedDepartmentId == Guid.Empty)
        {
            ErrorMessage = "Aktif departman bilgisi bulunamadi.";
            return;
        }

        if (SelectedAssignedUser is null)
        {
            ErrorMessage = "Gorevi atamak icin bir kullanici secin.";
            return;
        }

        if (SelectedPriority is null)
        {
            ErrorMessage = "Gorev onceligi secilmelidir.";
            return;
        }

        var trimmedTitle = TaskTitle.Trim();
        if (trimmedTitle.Length < 3 || trimmedTitle.Length > 120)
        {
            ErrorMessage = "Gorev basligi 3 ile 120 karakter arasinda olmalidir.";
            return;
        }

        var trimmedDescription = Description.Trim();
        if (trimmedDescription.Length < 5 || trimmedDescription.Length > 1000)
        {
            ErrorMessage = "Gorev aciklamasi 5 ile 1000 karakter arasinda olmalidir.";
            return;
        }

        var resolvedDeadline = DateOnly.FromDateTime(DeadlineDate.Date);
        if (resolvedDeadline < DateOnly.FromDateTime(DateTime.Today))
        {
            ErrorMessage = "Teslim tarihi bugunden once olamaz.";
            return;
        }

        try
        {
            IsCreatingTask = true;
            ErrorMessage = string.Empty;
            CreateStatus = string.Empty;

            var assignedUserName = SelectedAssignedUser.Name;
            var assignedUserId = SelectedAssignedUser.UserId;

            await projectManagementApiClient.CreateIndividualTaskCommandRequestAsync(new
            {
                AssignedUserId = assignedUserId,
                TaskTitle = trimmedTitle,
                Description = trimmedDescription,
                Deadline = resolvedDeadline,
                CompanyId = companyId,
                TaskPriorityCategoryId = SelectedPriority.Id
            });

            TaskTitle = string.Empty;
            Description = string.Empty;
            DeadlineDate = MinimumDeadlineDate.AddDays(1);
            SelectedAssignedUser = EligibleUsers.FirstOrDefault(item => item.UserId == assignedUserId)
                ?? EligibleUsers.FirstOrDefault();
            CreateStatus = $"Gorev {assignedUserName} kullanicisina basariyla atandi.";
            StatusText = string.IsNullOrWhiteSpace(CurrentDepartmentName)
                ? "Yeni bireysel gorev atandi."
                : $"{CurrentDepartmentName} departmanina yeni bireysel gorev atandi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Gorev olusturulamadi. Alanlari ve oncelik bilgisini kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Gorev olusturulamadi. Lutfen tekrar deneyin.");
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
            ErrorMessage = "Gorev olusturulurken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsCreatingTask = false;
        }
    }

    private void ConfigureManagedDepartment(WorkerReportAccessState accessState)
    {
        EligibleUsers.Clear();
        SelectedAssignedUser = null;
        managedDepartmentId = null;
        HasLeaderAccess = false;
        CurrentDepartmentName = string.Empty;
        AccessMessage = AccessDeniedMessageText;

        if (!accessState.CanAccessReportsPage ||
            accessState.DepartmentId is null ||
            accessState.DepartmentId.Value == Guid.Empty)
        {
            return;
        }

        managedDepartmentId = accessState.DepartmentId.Value;
        HasLeaderAccess = true;
        CurrentDepartmentName = string.IsNullOrWhiteSpace(accessState.DepartmentName)
            ? "Departman"
            : accessState.DepartmentName.Trim();
        AccessMessage = string.Empty;
        RefreshEligibleUsers();
    }

    private void RefreshEligibleUsers()
    {
        EligibleUsers.Clear();
        SelectedAssignedUser = null;

        if (managedDepartmentId is not Guid departmentId ||
            departmentId == Guid.Empty ||
            UserSession.UserId is not Guid currentUserId)
        {
            return;
        }

        var items = allCompanyUsers
            .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
            .Where(user => user.DepartmentMemberships.Any(membership => membership.DepartmentId == departmentId))
            .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase)
            .Select(user => new AssignableDepartmentUserOption
            {
                UserId = user.Id,
                Name = ResolveDisplayName(user),
                DepartmentName = CurrentDepartmentName
            })
            .ToList();

        foreach (var item in items)
        {
            EligibleUsers.Add(item);
        }

        SelectedAssignedUser = EligibleUsers.FirstOrDefault();
    }

    private void EnsurePriorityOptions()
    {
        if (PriorityOptions.Count > 0)
        {
            if (SelectedPriority is null)
            {
                SelectedPriority = PriorityOptions.FirstOrDefault(item => item.Id == 3) ?? PriorityOptions[0];
            }

            return;
        }

        PriorityOptions.Add(new TaskPriorityOption { Id = 1, DisplayName = "Oncelikli Gorev" });
        PriorityOptions.Add(new TaskPriorityOption { Id = 2, DisplayName = "Siradan Gorev" });
        PriorityOptions.Add(new TaskPriorityOption { Id = 3, DisplayName = "Acil Gorev" });
        SelectedPriority = PriorityOptions.FirstOrDefault(item => item.Id == 3) ?? PriorityOptions[0];
    }

    private void ApplyAccessDeniedState()
    {
        HasLeaderAccess = false;
        EligibleUsers.Clear();
        managedDepartmentId = null;
        SelectedAssignedUser = null;
        CurrentDepartmentName = string.Empty;
        AccessMessage = AccessDeniedMessageText;
        StatusText = string.Empty;
    }

    private static string ResolveDisplayName(CompanyUserDto user)
    {
        return string.IsNullOrWhiteSpace(user.Name)
            ? "Kullanici"
            : user.Name.Trim();
    }
}

public sealed record AssignableDepartmentUserOption
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;
}

public sealed record TaskPriorityOption
{
    public int Id { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
