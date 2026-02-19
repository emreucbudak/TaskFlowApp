using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Notification;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class NotificationsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    NotificationApiClient notificationApiClient) : PageViewModelBase(navigationService, userSession)
{
    public ObservableCollection<NotificationDto> Notifications { get; } = [];

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

            var response = await notificationApiClient.GetUserAllNotificationsAsync(UserSession.UserId.Value, 1, 20);

            Notifications.Clear();
            foreach (var item in response?.Items ?? [])
            {
                Notifications.Add(item);
            }

            StatusText = $"Toplam bildirim: {response?.TotalCount ?? 0}";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Bildirimler alinamadi ({ex.StatusCode}).";
        }
        catch (Exception)
        {
            ErrorMessage = "Bildirimler yuklenirken hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
