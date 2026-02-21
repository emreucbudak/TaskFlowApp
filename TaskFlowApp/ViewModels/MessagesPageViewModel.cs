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

            try
            {
                await signalRChatService.EnsureConnectedAsync();
            }
            catch
            {
                // Realtime baglanti kurulamasa da mesajlar API'den yuklenmeye devam eder.
            }

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
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Mesajlar alinamadi ({ex.StatusCode}).";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "API baglantisi kurulamadi. API servisinin calistigini kontrol edin.";
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "API yanit vermedi. Daha sonra tekrar deneyin.";
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

    public void RegisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
        signalRChatService.PrivateMessageReceived += OnPrivateMessageReceived;
    }

    public void UnregisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
    }

    private void OnPrivateMessageReceived(MessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Messages.Insert(0, message);
            UnreadCount += 1;
        });
    }

}
