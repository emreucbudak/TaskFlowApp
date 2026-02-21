using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Notification;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class NotificationsPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    NotificationApiClient notificationApiClient,
    ISignalRNotificationService signalRNotificationService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
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

            try
            {
                await signalRNotificationService.EnsureConnectedAsync();
            }
            catch
            {
                // SignalR baglantisi gecici olarak kurulamasa da bildirim listesi API'den yuklenmeye devam eder.
            }

            var response = await notificationApiClient.GetUserAllNotificationsAsync(UserSession.UserId.Value, 1, 20);
            var items = response?.Items ?? [];

            Notifications.Clear();
            foreach (var item in items)
            {
                Notifications.Add(item);
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
    private Task DisconnectRealtimeAsync() => signalRNotificationService.DisconnectAsync();

    public void RegisterRealtimeHandlers()
    {
        signalRNotificationService.NotificationReceived -= OnNotificationReceived;
        signalRNotificationService.NotificationReceived += OnNotificationReceived;
    }

    public void UnregisterRealtimeHandlers()
    {
        signalRNotificationService.NotificationReceived -= OnNotificationReceived;
    }

    private void OnNotificationReceived(NotificationDto item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Notifications.Insert(0, item);
        });
    }
}
