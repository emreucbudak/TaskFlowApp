using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    ChatApiClient chatApiClient) : PageViewModelBase(navigationService, userSession)
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
            StatusText = $"Mesaj: {Messages.Count} | Okunmamis: {UnreadCount}";
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
}
