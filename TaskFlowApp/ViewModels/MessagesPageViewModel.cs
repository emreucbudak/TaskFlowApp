using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ChatApiClient chatApiClient,
    ISignalRChatService signalRChatService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    public ObservableCollection<MessageDto> Messages { get; } = [];

    [ObservableProperty]
    private int unreadCount;

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
            await signalRChatService.EnsureConnectedAsync();

            var userId = UserSession.UserId.Value;
            var messagesTask = chatApiClient.GetMessagesByUserIdAsync(userId, 1, 20);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(userId);

            await Task.WhenAll(messagesTask, unreadTask);

            Messages.Clear();
            foreach (var message in await messagesTask ?? [])
            {
                Messages.Add(message);
            }

            UnreadCount = await unreadTask;
            StatusText = $"Mesaj: {Messages.Count} | Okunmamis: {UnreadCount} | SignalR: {(signalRChatService.IsConnected ? "Bagli" : "Bagli Degil")}";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Mesajlar alinamadi ({ex.StatusCode}).";
        }
        catch (Exception)
        {
            ErrorMessage = "Mesajlar yuklenirken hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private Task DisconnectRealtimeAsync() => signalRChatService.DisconnectAsync();

    partial void OnUnreadCountChanged(int value)
    {
        if (!string.IsNullOrWhiteSpace(StatusText))
        {
            StatusText = $"Mesaj: {Messages.Count} | Okunmamis: {value} | SignalR: {(signalRChatService.IsConnected ? "Bagli" : "Bagli Degil")}";
        }
    }

    public void RegisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
        signalRChatService.PrivateMessageReceived += OnPrivateMessageReceived;
        signalRChatService.ConnectionStateChanged -= OnConnectionStateChanged;
        signalRChatService.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public void UnregisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
        signalRChatService.ConnectionStateChanged -= OnConnectionStateChanged;
    }

    private void OnPrivateMessageReceived(MessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Insert(0, message);
            UnreadCount += 1;
        });
    }

    private void OnConnectionStateChanged(string state)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusText = $"Mesaj: {Messages.Count} | Okunmamis: {UnreadCount} | SignalR: {state}";
        });
    }
}
