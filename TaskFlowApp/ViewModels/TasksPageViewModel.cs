using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.ProjectManagement;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class TasksPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    ProjectManagementApiClient projectManagementApiClient) : PageViewModelBase(navigationService, userSession)
{
    public ObservableCollection<IndividualTaskDto> Tasks { get; } = [];

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

            var response = await projectManagementApiClient.GetIndividualTasksByUserIdAsync(UserSession.UserId.Value, 1, 20);

            Tasks.Clear();
            foreach (var task in response?.Items ?? [])
            {
                Tasks.Add(task);
            }

            StatusText = $"Toplam gorev: {response?.TotalCount ?? 0}";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Gorevler alinamadi ({ex.StatusCode}).";
        }
        catch (Exception)
        {
            ErrorMessage = "Gorevler yuklenirken hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
