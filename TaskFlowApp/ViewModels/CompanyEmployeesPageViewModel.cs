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
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class CompanyEmployeesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    IdentityApiClient identityApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const string InvalidPasswordMessage = "Şifre en az 8 karakter olmalı ve büyük harf, küçük harf, rakam içermelidir.";

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
    private string selectedDepartmentDisplayText = "Departman Seçin";

    [ObservableProperty]
    private DepartmentDto? selectedTransferDepartment;

    [ObservableProperty]
    private string selectedTransferDepartmentDisplayText = "Transfer Departmanı Seçin";

    [ObservableProperty]
    private CompanyUserDto? selectedUser;

    [ObservableProperty]
    private string selectedUserDisplayText = "Kullanıcı Seçin";

    [ObservableProperty]
    private CompanyUserDto? selectedDeleteUser;

    [ObservableProperty]
    private string selectedDeleteUserDisplayText = "Çalışan Seçin";

    [ObservableProperty]
    private CompanyUserDto? selectedPasswordUser;

    [ObservableProperty]
    private string selectedPasswordUserDisplayText = "Çalışan Seçin";

    [ObservableProperty]
    private string newPasswordInput = string.Empty;

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
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            var companyId = UserSession.CompanyId.Value;
            var departmentsTask = TryGetDepartmentsAsync(companyId);
            var usersTask = TryGetUsersAsync(companyId);

            await Task.WhenAll(departmentsTask, usersTask);

            ApplyDepartments(await departmentsTask);
            ApplyUsers(await usersTask);

            if (SelectedDepartment is not null && Departments.All(item => item.Id != SelectedDepartment.Id))
            {
                SelectedDepartment = null;
            }

            if (SelectedTransferDepartment is not null && Departments.All(item => item.Id != SelectedTransferDepartment.Id))
            {
                SelectedTransferDepartment = null;
            }

            if (SelectedUser is not null && CompanyUsers.All(item => item.Id != SelectedUser.Id))
            {
                SelectedUser = null;
            }

            if (SelectedDeleteUser is not null && CompanyUsers.All(item => item.Id != SelectedDeleteUser.Id))
            {
                SelectedDeleteUser = null;
            }

            if (SelectedPasswordUser is not null && CompanyUsers.All(item => item.Id != SelectedPasswordUser.Id))
            {
                SelectedPasswordUser = null;
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
            ErrorMessage = "Bir sorun oluştu. Lütfen tekrar deneyin.";
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
            ErrorMessage = "Şirket bilgisi bulunamadı. Tekrar giriş yapın.";
            return;
        }

        if (string.IsNullOrWhiteSpace(WorkerNameInput) ||
            string.IsNullOrWhiteSpace(WorkerEmailInput) ||
            string.IsNullOrWhiteSpace(WorkerPasswordInput))
        {
            ErrorMessage = "Çalışan eklemek için ad, e-posta ve şifre zorunludur.";
            return;
        }

        if (SelectedDepartment is null)
        {
            ErrorMessage = "Çalışan eklemek için departman seçin.";
            return;
        }

        if (!IsValidEmail(WorkerEmailInput))
        {
            ErrorMessage = "Geçerli bir e-posta girin.";
            return;
        }

        if (!IsValidPassword(WorkerPasswordInput))
        {
            ErrorMessage = InvalidPasswordMessage;
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
            var deptRefreshed = await TryRefreshDepartmentsAsync(companyId);
            var usersRefreshed = await TryRefreshUsersAsync(companyId);
            if (!deptRefreshed || !usersRefreshed)
            {
                ErrorMessage = "Çalışan eklendi ancak liste güncellenemedi. Sayfayı yenileyin.";
            }

            WorkerNameInput = string.Empty;
            WorkerEmailInput = string.Empty;
            WorkerPasswordInput = string.Empty;
            SelectedDepartment = null;
            FormMessage = "Çalışan başarıyla eklendi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            ErrorMessage = "Bu e-posta adresi zaten kullanılıyor.";
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            ErrorMessage = ex.ResponseBody?.Contains("zaten", StringComparison.OrdinalIgnoreCase) == true
                ? "Bu e-posta adresi zaten kullanılıyor."
                : ex.ResponseBody?.Contains("başarısız", StringComparison.OrdinalIgnoreCase) == true
                    ? "Kayıt işlemi başarısız oldu. Şifre kurallarını kontrol edin (en az 8 karakter, büyük harf, küçük harf, rakam)."
                    : "Çalışan eklenemedi. Bilgileri kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Çalışan eklenemedi. Alanları kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Çalışan eklenemedi. Lütfen tekrar deneyin.");
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
    private async Task TransferUserAsync()
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

        if (SelectedUser is null)
        {
            ErrorMessage = "Transfer için bir çalışan seçin.";
            return;
        }

        if (SelectedTransferDepartment is null)
        {
            ErrorMessage = "Transfer edilecek departmanı seçin.";
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
            var refreshed = await TryRefreshUsersAsync(companyId);
            if (!refreshed)
            {
                ErrorMessage = "Transfer kaydedildi ancak liste güncellenemedi. Sayfayı yenileyin.";
            }

            SelectedUser = null;
            SelectedTransferDepartment = null;
            FormMessage = "Çalışan başarıyla departmana transfer edildi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Transfer işlemi başarısız. Bilgileri kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Transfer işlemi başarısız. Lütfen tekrar deneyin.");
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
    private async Task AddDepartmentAsync()
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

        var departmentName = DepartmentNameInput?.Trim() ?? string.Empty;
        if (departmentName.Length < 2 || departmentName.Length > 10)
        {
            ErrorMessage = "Departman adı 2 ile 10 karakter arasında olmalıdır.";
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
            var departmentsRefreshed = await TryRefreshDepartmentsAsync(companyId);
            var departmentVisible = Departments.Any(item =>
                string.Equals(item.Name, departmentName, StringComparison.OrdinalIgnoreCase));

            if (!departmentsRefreshed || !departmentVisible)
            {
                ErrorMessage = "Departman kaydı alındı ancak liste güncellenemedi. Lütfen sayfayı yenileyip tekrar deneyin.";
                return;
            }

            DepartmentNameInput = string.Empty;
            FormMessage = "Departman başarıyla eklendi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Departman eklenemedi. Alanları kontrol edip tekrar deneyin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Departman eklenemedi. Lütfen tekrar deneyin.");
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
    private async Task RemoveWorkerAsync()
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

        if (SelectedDeleteUser is null)
        {
            ErrorMessage = "Silmek için bir çalışan seçin.";
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            FormMessage = string.Empty;

            await identityApiClient.DeleteWorkerCommandRequestAsync(new
            {
                UserId = SelectedDeleteUser.Id
            });

            var companyId = UserSession.CompanyId.Value;
            var refreshed = await TryRefreshUsersAsync(companyId);
            if (!refreshed)
            {
                ErrorMessage = "Çalışan silindi ancak liste güncellenemedi. Sayfayı yenileyin.";
            }

            SelectedDeleteUser = null;
            FormMessage = "Çalışan başarıyla silindi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            ErrorMessage = "Bu kullanıcı silinemiyor.";
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            ErrorMessage = "Çalışan bulunamadı.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Çalışan silinemedi. Lütfen tekrar deneyin.");
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
    private async Task ChangeWorkerPasswordAsync()
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

        if (SelectedPasswordUser is null)
        {
            ErrorMessage = "Şifresini değiştirmek için bir çalışan seçin.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPasswordInput))
        {
            ErrorMessage = "Yeni şifre alanı zorunludur.";
            return;
        }

        if (!IsValidPassword(NewPasswordInput))
        {
            ErrorMessage = InvalidPasswordMessage;
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            FormMessage = string.Empty;

            await identityApiClient.ChangeUserPasswordCommandRequestAsync(new
            {
                UserId = SelectedPasswordUser.Id,
                NewPassword = NewPasswordInput
            });

            NewPasswordInput = string.Empty;
            SelectedPasswordUser = null;
            FormMessage = "Çalışan şifresi başarıyla güncellendi.";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Yeni şifre API şifre kurallarına uymuyor. En az 8 karakter, büyük harf, küçük harf ve rakam kullanın.";
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            ErrorMessage = "Çalışan bulunamadı.";
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            ErrorMessage = "Bu çalışanın şifresi değiştirilemiyor.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Şifre güncellenemedi. Lütfen tekrar deneyin.");
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
        SelectedDepartmentDisplayText = value?.Name ?? "Departman Seçin";
    }

    partial void OnSelectedTransferDepartmentChanged(DepartmentDto? value)
    {
        SelectedTransferDepartmentDisplayText = value?.Name ?? "Transfer Departmanı Seçin";
    }

    partial void OnSelectedUserChanged(CompanyUserDto? value)
    {
        SelectedUserDisplayText = value?.Name ?? "Kullanıcı Seçin";
    }

    partial void OnSelectedDeleteUserChanged(CompanyUserDto? value)
    {
        SelectedDeleteUserDisplayText = value?.Name ?? "Çalışan Seçin";
    }

    partial void OnSelectedPasswordUserChanged(CompanyUserDto? value)
    {
        SelectedPasswordUserDisplayText = value?.Name ?? "Çalışan Seçin";
    }

}
