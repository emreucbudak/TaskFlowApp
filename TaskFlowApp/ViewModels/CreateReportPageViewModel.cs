using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public sealed partial class CreateReportPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ReportApiClient reportApiClient,
    IdentityApiClient identityApiClient,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService)
    : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    [ObservableProperty]
    private string titleInput = string.Empty;

    [ObservableProperty]
    private string descriptionInput = string.Empty;

    [ObservableProperty]
    private int selectedTopicId;

    [ObservableProperty]
    private string selectedTopicDisplayText = "Konu Seçin";

    [ObservableProperty]
    private DepartmentDto? selectedDepartment;

    [ObservableProperty]
    private string selectedDepartmentDisplayText = "Departman Seçin";

    [ObservableProperty]
    private string formMessage = string.Empty;

    public ObservableCollection<DepartmentDto> Departments { get; } = [];

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await LoadWorkerReportAccessStateAsync();

            var companyId = UserSession.CompanyId;
            if (companyId is null || companyId == Guid.Empty)
            {
                ErrorMessage = "Şirket bilgisi bulunamadı.";
                return;
            }

            var departments = await identityApiClient.GetAllCompanyDepartmentsAsync(companyId.Value);
            Departments.Clear();
            foreach (var department in departments)
            {
                Departments.Add(department);
            }
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, GenericLoadErrorMessage);
        }
        catch
        {
            ErrorMessage = GenericLoadErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateReportAsync()
    {
        if (IsBusy) return;

        if (string.IsNullOrWhiteSpace(TitleInput))
        {
            FormMessage = "Başlık alanı boş bırakılamaz.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DescriptionInput) || DescriptionInput.Trim().Length < 10)
        {
            FormMessage = "Açıklama en az 10 karakter olmalıdır.";
            return;
        }

        if (SelectedTopicId <= 0)
        {
            FormMessage = "Lütfen bir rapor konusu seçin.";
            return;
        }

        if (SelectedDepartment is null)
        {
            FormMessage = "Lütfen bildirilecek departmanı seçin.";
            return;
        }

        var userId = UserSession.UserId;
        var companyId = UserSession.CompanyId;

        if (userId is null || companyId is null)
        {
            FormMessage = "Oturum bilgileri eksik. Lütfen yeniden giriş yapın.";
            return;
        }

        IsBusy = true;
        ErrorMessage = string.Empty;

        try
        {
            await reportApiClient.CreateReportCommandRequestAsync(new
            {
                ReportTopicId = SelectedTopicId,
                Title = TitleInput.Trim(),
                Description = DescriptionInput.Trim(),
                UserId = userId.Value,
                ReportStatusId = 1,
                NotifiedDepartmentId = SelectedDepartment.Id,
                CompanyId = companyId.Value
            });

            TitleInput = string.Empty;
            DescriptionInput = string.Empty;
            SelectedTopicId = 0;
            SelectedTopicDisplayText = "Konu Seçin";
            SelectedDepartment = null;
            SelectedDepartmentDisplayText = "Departman Seçin";

            FormMessage = "Rapor başarıyla oluşturuldu.";
        }
        catch (ApiException ex)
        {
            FormMessage = ResolveApiErrorMessage(ex, "Rapor oluşturulurken bir hata oluştu. Lütfen tekrar deneyin.");
        }
        catch
        {
            FormMessage = GenericConnectionErrorMessage;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
