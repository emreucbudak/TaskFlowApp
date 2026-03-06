using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Infrastructure.Navigation;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Models.Identity;
using TaskFlowApp.Services.ApiClients;
using TaskFlowApp.Services.Realtime;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient,
    ISignalRChatService signalRChatService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
{
    private const int MessagePageSize = 200;

    private readonly List<MessageDto> allMessages = [];
    private readonly List<MessageConversationUserItem> allUsers = [];

    public ObservableCollection<MessageConversationUserItem> VisibleUsers { get; } = [];
    public ObservableCollection<ConversationMessageItem> ConversationMessages { get; } = [];

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private MessageConversationUserItem? selectedConversationUser;

    [ObservableProperty]
    private string messageDraft = string.Empty;

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

        if (UserSession.CompanyId is null)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
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

            var currentUserId = UserSession.UserId.Value;
            var companyId = UserSession.CompanyId.Value;
            var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
            var messagesTask = chatApiClient.GetMessagesByUserIdAsync(currentUserId, 1, MessagePageSize);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(currentUserId);

            await Task.WhenAll(usersTask, messagesTask, unreadTask);

            allUsers.Clear();
            allUsers.AddRange((await usersTask ?? [])
                .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
                .OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
                .Select(MapUser));

            allMessages.Clear();
            allMessages.AddRange((await messagesTask ?? [])
                .Where(message => !message.IsDeleted)
                .OrderBy(message => message.SendTime));

            UnreadCount = await unreadTask;
            ApplyUserFilter();
            RestoreOrSelectConversation();
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
    private Task SelectConversationUserAsync(MessageConversationUserItem? user)
    {
        SelectConversationUser(user);
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void SendMessage()
    {
        if (UserSession.UserId is not Guid currentUserId ||
            SelectedConversationUser is null ||
            string.IsNullOrWhiteSpace(MessageDraft))
        {
            return;
        }

        var message = new MessageDto
        {
            Id = Guid.NewGuid(),
            Content = MessageDraft.Trim(),
            SendTime = DateTime.UtcNow,
            SenderId = currentUserId,
            ReceiverId = SelectedConversationUser.UserId,
            IsDelivered = true,
            IsRead = true
        };

        allMessages.Add(message);
        allMessages.Sort((left, right) => left.SendTime.CompareTo(right.SendTime));
        ConversationMessages.Add(MapConversationMessage(message, currentUserId));
        MessageDraft = string.Empty;
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

    partial void OnSearchTextChanged(string value)
    {
        ApplyUserFilter();
    }

    private void ApplyUserFilter()
    {
        VisibleUsers.Clear();

        IEnumerable<MessageConversationUserItem> filteredUsers = allUsers;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filteredUsers = filteredUsers.Where(user =>
                user.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var user in filteredUsers)
        {
            VisibleUsers.Add(user);
        }
    }

    private void RestoreOrSelectConversation()
    {
        if (allUsers.Count == 0)
        {
            SelectConversationUser(null);
            return;
        }

        var selectedUserId = SelectedConversationUser?.UserId;
        var resolvedSelection = allUsers.FirstOrDefault(user => user.UserId == selectedUserId)
            ?? VisibleUsers.FirstOrDefault()
            ?? allUsers.First();

        SelectConversationUser(resolvedSelection);
    }

    private void SelectConversationUser(MessageConversationUserItem? user)
    {
        foreach (var item in allUsers)
        {
            item.IsSelected = user is not null && item.UserId == user.UserId;
        }

        SelectedConversationUser = user;
        MessageDraft = string.Empty;

        if (user is null)
        {
            ConversationMessages.Clear();
            return;
        }

        RebuildConversation();
    }

    private void RebuildConversation()
    {
        ConversationMessages.Clear();

        var currentUserId = UserSession.UserId;
        var selectedUserId = SelectedConversationUser?.UserId;
        if (currentUserId is null || selectedUserId is null)
        {
            return;
        }

        var conversationItems = allMessages
            .Where(message => IsConversationMessage(message, currentUserId.Value, selectedUserId.Value))
            .OrderBy(message => message.SendTime)
            .Select(message => MapConversationMessage(message, currentUserId.Value))
            .ToList();

        foreach (var item in conversationItems)
        {
            ConversationMessages.Add(item);
        }
    }

    private void OnPrivateMessageReceived(MessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (message.IsDeleted)
            {
                return;
            }

            allMessages.Add(message);
            allMessages.Sort((left, right) => left.SendTime.CompareTo(right.SendTime));

            if (SelectedConversationUser is not null &&
                UserSession.UserId is Guid currentUserId &&
                IsConversationMessage(message, currentUserId, SelectedConversationUser.UserId))
            {
                ConversationMessages.Add(MapConversationMessage(message, currentUserId));
                return;
            }

            UnreadCount += 1;
        });
    }

    private static bool IsConversationMessage(MessageDto message, Guid currentUserId, Guid otherUserId)
    {
        if (message.GroupId is not null)
        {
            return false;
        }

        return (message.SenderId == currentUserId && message.ReceiverId == otherUserId)
            || (message.SenderId == otherUserId && message.ReceiverId == currentUserId)
            || (message.SenderId == otherUserId && message.ReceiverId is null);
    }

    private static MessageConversationUserItem MapUser(CompanyUserDto user)
    {
        var trimmedName = string.IsNullOrWhiteSpace(user.Name) ? "Kullanici" : user.Name.Trim();
        var initials = string.Concat(trimmedName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

        return new MessageConversationUserItem
        {
            UserId = user.Id,
            Name = trimmedName,
            Initials = string.IsNullOrWhiteSpace(initials) ? "K" : initials
        };
    }

    private static ConversationMessageItem MapConversationMessage(MessageDto message, Guid currentUserId)
    {
        return new ConversationMessageItem
        {
            Content = message.Content,
            SentAtText = FormatTimestamp(message.SendTime),
            IsOwnMessage = message.SenderId == currentUserId
        };
    }

    private static string FormatTimestamp(DateTime value)
    {
        var localValue = value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };

        return localValue.ToString("dd.MM.yyyy HH:mm");
    }
}

public partial class MessageConversationUserItem : ObservableObject
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}

public sealed record ConversationMessageItem
{
    public string Content { get; init; } = string.Empty;
    public string SentAtText { get; init; } = string.Empty;
    public bool IsOwnMessage { get; init; }
}
