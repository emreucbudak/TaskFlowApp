using System.Collections.ObjectModel;
using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class CompanyEmployeesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<CompanyGroupDto> Groups { get; } = [];
    public ObservableCollection<DepartmentDto> Departments { get; } = [];
    public ObservableCollection<CompanyUserDto> CompanyUsers { get; } = [];

    [ObservableProperty]
    private string workerNameInput = string.Empty;

    [ObservableProperty]
    private string workerEmailInput = string.Empty;

    [ObservableProperty]
    private string workerPasswordInput = string.Empty;

    [ObservableProperty]
    private string departmentNameInput = string.Empty;

    [ObservableProperty]
    private DepartmentDto? selectedDepartment;

    [ObservableProperty]
    private string selectedDepartmentDisplayText = "Departman Secin";

    [ObservableProperty]
    private DepartmentDto? selectedTransferDepartment;

    [ObservableProperty]
    private string selectedTransferDepartmentDisplayText = "Transfer Departman Secin";

    [ObservableProperty]
    private CompanyUserDto? selectedUser;

    [ObservableProperty]
    private string selectedUserDisplayText = "Kullanıcı Seçin";

    [ObservableProperty]
    private string formMessage = string.Empty;

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
            var groupsTask = identityApiClient.GetAllCompanyGroupsAsync(companyId);
            var departmentsTask = TryGetDepartmentsAsync(companyId);
            var usersTask = TryGetUsersAsync(companyId);

            await Task.WhenAll(groupsTask, departmentsTask, usersTask);

            ApplyGroups((await groupsTask) ?? []);
            ApplyDepartments(await departmentsTask);
            ApplyUsers(await usersTask);

            if (SelectedDepartment is not null && Departments.All(item => item.Id != SelectedDepartment.Id))
            {
                SelectedDepartment = null;
            }

            if (string.IsNullOrWhiteSpace(FormMessage))
            {
                FormMessage = string.Empty;
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
    private async Task AddWorkerAsync()
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

        if (string.IsNullOrWhiteSpace(WorkerNameInput) ||
            string.IsNullOrWhiteSpace(WorkerEmailInput) ||
            string.IsNullOrWhiteSpace(WorkerPasswordInput))
        {
            ErrorMessage = "Calisan eklemek icin ad, e-posta ve sifre zorunludur.";
            return;
        }

        if (SelectedDepartment is null)
        {
            ErrorMessage = "Calisan eklemek icin departman secin.";
            return;
        }

        if (!IsValidEmail(WorkerEmailInput))
        {
            ErrorMessage = "Gecerli bir e-posta girin.";
            return;
        }

        if (!IsValidPassword(WorkerPasswordInput))
        {
            ErrorMessage = "Sifre en az 8 karakter olmali ve buyuk harf, kucuk harf, rakam icermelidir.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            FormMessage = string.Empty;

            await identityApiClient.RegisterCommandRequestAsync(new
            {
                Name = WorkerNameInput.Trim(),
                Email = WorkerEmailInput.Trim(),
                Password = WorkerPasswordInput,
                CompanyId = UserSession.CompanyId.Value,
                Role = "Worker",
                DepartmentId = SelectedDepartment.Id
            });

            var companyId = UserSession.CompanyId.Value;
            await RefreshGroupsAsync(companyId);
            _ = await TryRefreshDepartmentsAsync(companyId);
            _ = await TryRefreshUsersAsync(companyId);

            WorkerNameInput = string.Empty;
            WorkerEmailInput = string.Empty;
            WorkerPasswordInput = string.Empty;
            SelectedDepartment = null;
            FormMessage = "Calisan basariyla eklendi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            ErrorMessage = "Bu e-posta adresi zaten kullaniliyor.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Calisan eklenemedi. Alanlari kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Calisan eklenemedi. Lutfen tekrar deneyin.");
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
    private async Task TransferUserAsync()
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

        if (SelectedUser is null)
        {
            ErrorMessage = "Transfer icin bir calisan secin.";
            return;
        }

        if (SelectedTransferDepartment is null)
        {
            ErrorMessage = "Transfer edilecek departmani secin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            FormMessage = string.Empty;

            await identityApiClient.AddUserToDepartmentCommandRequestAsync(new
            {
                UserId = SelectedUser.Id,
                DepartmentId = SelectedTransferDepartment.Id,
                RoleId = 3
            });

            var companyId = UserSession.CompanyId.Value;
            await RefreshGroupsAsync(companyId);
            _ = await TryRefreshUsersAsync(companyId);

            FormMessage = "Calisan basariyla departmana transfer edildi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Transfer islemi basarisiz. Bilgileri kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Transfer islemi basarisiz. Lutfen tekrar deneyin.");
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
    private async Task AddDepartmentAsync()
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

        var departmentName = DepartmentNameInput?.Trim() ?? string.Empty;
        if (departmentName.Length < 2 || departmentName.Length > 10)
        {
            ErrorMessage = "Departman adi 2 ile 10 karakter arasinda olmalidir.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            FormMessage = string.Empty;

            await identityApiClient.AddDepartmentCommandRequestAsync(new
            {
                Name = departmentName,
                CompanyId = UserSession.CompanyId.Value
            });

            var companyId = UserSession.CompanyId.Value;
            await RefreshGroupsAsync(companyId);
            var departmentsRefreshed = await TryRefreshDepartmentsAsync(companyId);
            var departmentVisible = Departments.Any(item =>
                string.Equals(item.Name, departmentName, StringComparison.OrdinalIgnoreCase));

            if (!departmentsRefreshed || !departmentVisible)
            {
                ErrorMessage = "Departman kaydi alindi ancak liste guncellenemedi. Lutfen sayfayi yenileyip tekrar deneyin.";
                return;
            }

            DepartmentNameInput = string.Empty;
            FormMessage = "Departman basariyla eklendi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Departman eklenemedi. Alanlari kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Departman eklenemedi. Lutfen tekrar deneyin.");
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

    private async Task RefreshGroupsAsync(Guid companyId)
    {
        var response = await identityApiClient.GetAllCompanyGroupsAsync(companyId);
        ApplyGroups(response ?? []);
    }

    private async Task RefreshDepartmentsAsync(Guid companyId)
    {
        var response = await identityApiClient.GetAllCompanyDepartmentsAsync(companyId);
        ApplyDepartments(response ?? []);
    }

    private async Task RefreshUsersAsync(Guid companyId)
    {
        var response = await identityApiClient.GetAllCompanyUsersAsync(companyId);
        ApplyUsers(response ?? []);
    }

    private async Task<List<DepartmentDto>> TryGetDepartmentsAsync(Guid companyId)
    {
        try
        {
            return await identityApiClient.GetAllCompanyDepartmentsAsync(companyId) ?? [];
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

    private async Task<List<CompanyUserDto>> TryGetUsersAsync(Guid companyId)
    {
        try
        {
            return await identityApiClient.GetAllCompanyUsersAsync(companyId) ?? [];
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

    private void ApplyGroups(IEnumerable<CompanyGroupDto> groups)
    {
        var normalizedGroups = NormalizeGroups(groups);

        Groups.Clear();
        foreach (var group in normalizedGroups)
        {
            Groups.Add(group);
        }
    }

    private void ApplyDepartments(IEnumerable<DepartmentDto> departments)
    {
        Departments.Clear();
        foreach (var department in departments.OrderBy(item => item.Name))
        {
            Departments.Add(department);
        }
    }

    private void ApplyUsers(IEnumerable<CompanyUserDto> users)
    {
        CompanyUsers.Clear();
        foreach (var user in users.OrderBy(item => item.Name))
        {
            CompanyUsers.Add(user);
        }
    }

    private async Task<bool> TryRefreshUsersAsync(Guid companyId)
    {
        try
        {
            await RefreshUsersAsync(companyId);
            return true;
        }
        catch (ApiException)
        {
            CompanyUsers.Clear();
            return false;
        }
        catch (HttpRequestException)
        {
            CompanyUsers.Clear();
            return false;
        }
        catch (TaskCanceledException)
        {
            CompanyUsers.Clear();
            return false;
        }
    }

    private async Task<bool> TryRefreshDepartmentsAsync(Guid companyId)
    {
        try
        {
            await RefreshDepartmentsAsync(companyId);
            return true;
        }
        catch (ApiException)
        {
            Departments.Clear();
            return false;
        }
        catch (HttpRequestException)
        {
            Departments.Clear();
            return false;
        }
        catch (TaskCanceledException)
        {
            Departments.Clear();
            return false;
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsValidPassword(string password)
    {
        return password.Length >= 8
            && password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }

    partial void OnSelectedDepartmentChanged(DepartmentDto? value)
    {
        SelectedDepartmentDisplayText = value?.Name ?? "Departman Secin";
    }

    partial void OnSelectedTransferDepartmentChanged(DepartmentDto? value)
    {
        SelectedTransferDepartmentDisplayText = value?.Name ?? "Transfer Departman Secin";
    }

    partial void OnSelectedUserChanged(CompanyUserDto? value)
    {
        SelectedUserDisplayText = value?.Name ?? "Kullanıcı Seçin";
    }

    private static List<CompanyGroupDto> NormalizeGroups(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupName))
            .Where(group => !group.GroupName.Trim().StartsWith("ATF", StringComparison.OrdinalIgnoreCase))
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
