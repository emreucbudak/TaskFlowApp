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
            var realtimeState = "Disconnected";

            try
            {
                await signalRNotificationService.EnsureConnectedAsync();
                realtimeState = signalRNotificationService.IsConnected ? "Connected" : "Disconnected";
            }
            catch
            {
                realtimeState = "ConnectionFailed";
            }

            var response = await notificationApiClient.GetUserAllNotificationsAsync(UserSession.UserId.Value, 1, 20);

            Notifications.Clear();
            foreach (var item in response.Items)
            {
                Notifications.Add(item);
            }

            StatusText = $"Toplam bildirim: {response.TotalCount} | SignalR: {realtimeState}";
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

    [RelayCommand]
    private Task DisconnectRealtimeAsync() => signalRNotificationService.DisconnectAsync();

    public void RegisterRealtimeHandlers()
    {
        signalRNotificationService.NotificationReceived -= OnNotificationReceived;
        signalRNotificationService.NotificationReceived += OnNotificationReceived;
        signalRNotificationService.ConnectionStateChanged -= OnConnectionStateChanged;
        signalRNotificationService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public void UnregisterRealtimeHandlers()
    {
        signalRNotificationService.NotificationReceived -= OnNotificationReceived;
        signalRNotificationService.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    private void OnNotificationReceived(NotificationDto item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Notifications.Insert(0, item);
            StatusText = $"Toplam bildirim: {Notifications.Count} | SignalR: Connected";
        });
    }

    private void OnConnectionStateChanged(string state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = $"Toplam bildirim: {Notifications.Count} | SignalR: {state}";
        });
    }
}
