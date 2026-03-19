using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Chat;
using TaskFlowApp.Services.State;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel
{
    public ObservableCollection<ConversationMessageItem> ConversationMessages { get; } = [];

    [ObservableProperty]
    private string messageDraft = string.Empty;

    [ObservableProperty]
    private bool isSendingMessage;

    [ObservableProperty]
    private bool isConversationLoading;

    [ObservableProperty]
    private string selectedConversationTitle = string.Empty;

    [ObservableProperty]
    private string selectedConversationSubtitle = string.Empty;

    [ObservableProperty]
    private string conversationEmptyMessage = string.Empty;

    [ObservableProperty]
    private string conversationStatusMessage = string.Empty;

    [ObservableProperty]
    private bool hasSelectedConversation;

    [RelayCommand]
    private async Task SelectConversationUserAsync(MessageConversationUserItem? user)
    {
        conversationLoadCancellationTokenSource?.Cancel();
        conversationLoadCancellationTokenSource?.Dispose();
        var currentCancellationSource = new CancellationTokenSource();
        conversationLoadCancellationTokenSource = currentCancellationSource;
        var cancellationToken = currentCancellationSource.Token;

        if (user is null || UserSession.UserId is not Guid currentUserId)
        {
            ApplySelectedConversationState(null);
            ConversationMessages.Clear();
            currentCancellationSource.Dispose();
            if (ReferenceEquals(conversationLoadCancellationTokenSource, currentCancellationSource))
            {
                conversationLoadCancellationTokenSource = null;
            }
            return;
        }

        try
        {
            ApplySelectedConversationState(user);
            ErrorMessage = string.Empty;

            var cachedMessages = GetCachedConversationMessages(currentUserId, user.UserId);
            if (cachedMessages.Count > 0)
            {
                SyncConversationMessages(cachedMessages, currentUserId);
                await MarkConversationAsReadAsync(currentUserId, user.UserId, adjustUnreadCount: true, cancellationToken);
                return;
            }

            ConversationMessages.Clear();
            IsConversationLoading = true;

            var conversationMessages = await GetConversationMessagesAsync(currentUserId, user.UserId, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ReplaceConversationCache(currentUserId, user.UserId, conversationMessages);
            ReplaceConversationMessages(conversationMessages, currentUserId);
            await MarkConversationAsReadAsync(currentUserId, user.UserId, adjustUnreadCount: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, GenericLoadErrorMessage);
            ConversationMessages.Clear();
            ConversationEmptyMessage = "Sohbet su anda yuklenemiyor.";
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
            ConversationMessages.Clear();
            ConversationEmptyMessage = "Sohbet su anda yuklenemiyor.";
        }
        catch (Exception)
        {
            ErrorMessage = "Bir sorun olustu. Lutfen tekrar deneyin.";
            ConversationMessages.Clear();
            ConversationEmptyMessage = "Sohbet su anda yuklenemiyor.";
        }
        finally
        {
            if (ReferenceEquals(conversationLoadCancellationTokenSource, currentCancellationSource))
            {
                conversationLoadCancellationTokenSource = null;
                IsConversationLoading = false;
            }

            currentCancellationSource.Dispose();
        }
    }

    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (IsSendingMessage ||
            UserSession.UserId is not Guid currentUserId ||
            SelectedConversationUser is null ||
            string.IsNullOrWhiteSpace(MessageDraft))
        {
            return;
        }

        var selectedUser = SelectedConversationUser;
        var content = MessageDraft.Trim();

        try
        {
            IsSendingMessage = true;
            ErrorMessage = string.Empty;

            await chatApiClient.CreateMessageCommandRequestAsync(new
            {
                Content = content,
                SenderId = currentUserId,
                ReceiverId = selectedUser.UserId,
                GroupId = (Guid?)null
            });

            MessageDraft = string.Empty;
            await RefreshConversationAfterSendAsync(currentUserId, selectedUser.UserId, selectedUser.Name, content);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, GenericLoadErrorMessage);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Mesaj gonderilirken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsSendingMessage = false;
        }
    }

    [RelayCommand]
    private async Task DeleteMessageAsync(ConversationMessageItem? messageItem)
    {
        if (messageItem is null ||
            messageItem.IsDeleting ||
            !messageItem.IsOwnMessage ||
            messageItem.MessageId == Guid.Empty ||
            UserSession.UserId is not Guid currentUserId ||
            SelectedConversationUser is null)
        {
            return;
        }

        var selectedUser = SelectedConversationUser;

        try
        {
            messageItem.IsDeleting = true;
            ErrorMessage = string.Empty;

            await chatApiClient.DeleteMessageCommandRequestAsync(new
            {
                Id = messageItem.MessageId
            });

            MarkCachedMessageAsDeleted(messageItem.MessageId);
            var cachedMessages = GetCachedConversationMessages(currentUserId, selectedUser.UserId);

            if (SelectedConversationUser?.UserId == selectedUser.UserId)
            {
                SyncConversationMessages(cachedMessages, currentUserId);
            }

            RefreshPriorConversationUserFromCache(currentUserId, selectedUser.UserId, selectedUser.Name);

        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Mesaj silinemedi. Lutfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Mesaj silinirken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            messageItem.IsDeleting = false;
            messageItem.IsDeleteVisible = false;
        }
    }

    private async Task RefreshConversationAfterSendAsync(
        Guid currentUserId,
        Guid otherUserId,
        string? fallbackName,
        string content)
    {
        try
        {
            var conversationMessages = await GetConversationMessagesAsync(currentUserId, otherUserId);
            if (conversationMessages.Count == 0)
            {
                throw new InvalidOperationException("Mesaj gonderildikten sonra sohbet yenilenemedi.");
            }

            ReplaceConversationCache(currentUserId, otherUserId, conversationMessages);

            if (SelectedConversationUser?.UserId == otherUserId)
            {
                SyncConversationMessages(conversationMessages, currentUserId);
            }

            RefreshPriorConversationUserFromCache(currentUserId, otherUserId, fallbackName);
        }
        catch
        {
            var fallbackMessage = new MessageDto
            {
                Id = Guid.NewGuid(),
                Content = content,
                SendTime = DateTime.UtcNow,
                SenderId = currentUserId,
                ReceiverId = otherUserId,
                IsDelivered = true,
                IsRead = true
            };

            UpsertDirectMessageCache(fallbackMessage);

            if (SelectedConversationUser?.UserId == otherUserId)
            {
                ConversationMessages.Add(MapConversationMessage(fallbackMessage, currentUserId));
                ConversationEmptyMessage = string.Empty;
            }

            UpsertPriorConversationUser(fallbackMessage, otherUserId, fallbackName);
        }
    }

    private async Task<List<MessageDto>> GetConversationMessagesAsync(
        Guid currentUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        var messages = await chatApiClient.GetMessagesBetweenUsersAsync(
            currentUserId,
            currentUserId,
            otherUserId,
            1,
            MessagePageSize,
            cancellationToken);

        return (messages ?? [])
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .OrderBy(message => message.SendTime)
            .ToList();
    }

    private void CacheConversationMessages(IEnumerable<MessageDto> messages)
    {
        foreach (var message in messages)
        {
            UpsertDirectMessageCache(message);
        }
    }

    private void ReplaceConversationCache(Guid currentUserId, Guid otherUserId, IEnumerable<MessageDto> messages)
    {
        recentDirectMessages.RemoveAll(message =>
            message.GroupId is null &&
            ((message.SenderId == currentUserId && message.ReceiverId == otherUserId) ||
             (message.SenderId == otherUserId && message.ReceiverId == currentUserId)));

        foreach (var message in messages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .OrderByDescending(message => message.SendTime))
        {
            recentDirectMessages.Add(message);
        }

        recentDirectMessages.Sort((left, right) => right.SendTime.CompareTo(left.SendTime));
    }

    private void UpsertDirectMessageCache(MessageDto message)
    {
        if (message.IsDeleted || message.GroupId is not null)
        {
            return;
        }

        var existingIndex = recentDirectMessages.FindIndex(item => item.Id == message.Id);
        if (existingIndex >= 0)
        {
            recentDirectMessages[existingIndex] = message;
        }
        else
        {
            recentDirectMessages.Add(message);
        }

        recentDirectMessages.Sort((left, right) => right.SendTime.CompareTo(left.SendTime));
    }

    private List<MessageDto> GetCachedConversationMessages(Guid currentUserId, Guid otherUserId)
    {
        return recentDirectMessages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .Where(message =>
                (message.SenderId == currentUserId && message.ReceiverId == otherUserId) ||
                (message.SenderId == otherUserId && message.ReceiverId == currentUserId))
            .OrderBy(message => message.SendTime)
            .ToList();
    }

    private void MarkCachedMessageAsDeleted(Guid messageId)
    {
        var existingIndex = recentDirectMessages.FindIndex(item => item.Id == messageId);
        if (existingIndex < 0)
        {
            return;
        }

        recentDirectMessages[existingIndex] = recentDirectMessages[existingIndex] with
        {
            IsDeleted = true
        };
    }

    private void MarkCachedConversationAsRead(Guid currentUserId, Guid otherUserId)
    {
        for (var index = 0; index < recentDirectMessages.Count; index++)
        {
            var message = recentDirectMessages[index];
            if (message.IsDeleted ||
                message.GroupId is not null ||
                message.ReceiverId != currentUserId ||
                message.SenderId != otherUserId ||
                message.IsRead)
            {
                continue;
            }

            recentDirectMessages[index] = message with
            {
                IsRead = true
            };
        }
    }

    private async Task MarkConversationAsReadAsync(
        Guid currentUserId,
        Guid otherUserId,
        bool adjustUnreadCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var markedCount = await chatApiClient.MarkConversationAsReadAsync(otherUserId, cancellationToken);
            if (markedCount <= 0)
            {
                return;
            }

            MarkCachedConversationAsRead(currentUserId, otherUserId);

            if (SelectedConversationUser?.UserId == otherUserId)
            {
                var cachedMessages = GetCachedConversationMessages(currentUserId, otherUserId);
                SyncConversationMessages(cachedMessages, currentUserId);
            }

            if (adjustUnreadCount)
            {
                UnreadCount = Math.Max(0, UnreadCount - markedCount);
                workerDashboardStateService.DecrementUnreadMessageCount(markedCount);
            }
        }
        catch
        {
        }
    }

    private Task MarkConversationAsReadForActiveConversationAsync(Guid currentUserId, Guid otherUserId) =>
        MarkConversationAsReadAsync(currentUserId, otherUserId, adjustUnreadCount: false);

    private void ReplaceConversationMessages(IEnumerable<MessageDto> messages, Guid currentUserId)
    {
        ConversationMessages.Clear();

        foreach (var item in messages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .OrderBy(message => message.SendTime)
            .Select(message => MapConversationMessage(message, currentUserId)))
        {
            ConversationMessages.Add(item);
        }

        ConversationEmptyMessage = ConversationMessages.Count == 0
            ? NewConversationMessage
            : string.Empty;
    }

    private void SyncConversationMessages(IEnumerable<MessageDto> messages, Guid currentUserId)
    {
        var desiredMessages = messages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .OrderBy(message => message.SendTime)
            .Select(message => MapConversationMessage(message, currentUserId))
            .ToList();

        var desiredIds = desiredMessages
            .Select(item => item.MessageId)
            .ToHashSet();

        for (var index = ConversationMessages.Count - 1; index >= 0; index--)
        {
            if (!desiredIds.Contains(ConversationMessages[index].MessageId))
            {
                ConversationMessages.RemoveAt(index);
            }
        }

        for (var desiredIndex = 0; desiredIndex < desiredMessages.Count; desiredIndex++)
        {
            var desiredItem = desiredMessages[desiredIndex];

            if (desiredIndex < ConversationMessages.Count &&
                ConversationMessages[desiredIndex].MessageId == desiredItem.MessageId)
            {
                continue;
            }

            var existingIndex = -1;
            for (var currentIndex = 0; currentIndex < ConversationMessages.Count; currentIndex++)
            {
                if (ConversationMessages[currentIndex].MessageId == desiredItem.MessageId)
                {
                    existingIndex = currentIndex;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                var existingItem = ConversationMessages[existingIndex];
                existingItem.IsDeleteVisible = false;
                existingItem.IsDeleting = false;
                ConversationMessages.RemoveAt(existingIndex);
                ConversationMessages.Insert(desiredIndex, existingItem);
                continue;
            }

            ConversationMessages.Insert(desiredIndex, desiredItem);
        }

        while (ConversationMessages.Count > desiredMessages.Count)
        {
            ConversationMessages.RemoveAt(ConversationMessages.Count - 1);
        }

        ConversationEmptyMessage = ConversationMessages.Count == 0
            ? NewConversationMessage
            : string.Empty;
    }

    private void OnPrivateMessageReceived(MessageDto message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (message.IsDeleted || message.GroupId is not null || UserSession.UserId is not Guid currentUserId)
            {
                return;
            }

            var otherUserId = ResolveOtherUserId(message, currentUserId);
            if (!otherUserId.HasValue || otherUserId.Value == Guid.Empty)
            {
                return;
            }

            var isActiveConversation = SelectedConversationUser?.UserId == otherUserId.Value;
            var normalizedMessage = isActiveConversation && message.ReceiverId == currentUserId
                ? message with { IsRead = true }
                : message;

            UpsertDirectMessageCache(normalizedMessage);
            UpsertPriorConversationUser(normalizedMessage, otherUserId.Value);

            if (isActiveConversation)
            {
                if (ConversationMessages.All(item => item.MessageId != normalizedMessage.Id))
                {
                    ConversationMessages.Add(MapConversationMessage(normalizedMessage, currentUserId));
                }

                ConversationEmptyMessage = string.Empty;

                if (message.ReceiverId == currentUserId)
                {
                    _ = MarkConversationAsReadForActiveConversationAsync(currentUserId, otherUserId.Value);
                }

                return;
            }

            if (message.ReceiverId == currentUserId)
            {
                UnreadCount += 1;
                workerDashboardStateService.IncrementUnreadMessageCount();
            }
        });
    }

    private void UpsertPriorConversationUser(MessageDto message, Guid otherUserId, string? fallbackName = null)
    {
        UpdateConversationUserSummary(otherUserId, message, fallbackName);
    }

    private void RefreshPriorConversationUserFromCache(Guid currentUserId, Guid otherUserId, string? fallbackName = null)
    {
        var latestMessage = recentDirectMessages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .Where(message =>
                (message.SenderId == currentUserId && message.ReceiverId == otherUserId) ||
                (message.SenderId == otherUserId && message.ReceiverId == currentUserId))
            .OrderByDescending(message => message.SendTime)
            .FirstOrDefault();

        UpdateConversationUserSummary(otherUserId, latestMessage, fallbackName);
    }

    private void UpdateConversationUserSummary(Guid otherUserId, MessageDto? latestMessage, string? fallbackName = null)
    {
        var updatedItem = BuildConversationUserItem(otherUserId, ResolveDisplayName(otherUserId, fallbackName), latestMessage);
        priorConversationUserMap[otherUserId] = updatedItem;

        var existingIndex = priorConversationUsers.FindIndex(item => item.UserId == otherUserId);
        if (existingIndex >= 0)
        {
            priorConversationUsers[existingIndex] = updatedItem;
        }
        else
        {
            priorConversationUsers.Add(updatedItem);
        }

        priorConversationUsers.Sort((left, right) =>
        {
            var leftValue = left.LastActivityUtc ?? DateTime.MinValue;
            var rightValue = right.LastActivityUtc ?? DateTime.MinValue;
            var compare = rightValue.CompareTo(leftValue);
            return compare != 0
                ? compare
                : StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
        });

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            ApplyVisibleUsers(priorConversationUsers);
        }

        if (SelectedConversationUser?.UserId == otherUserId)
        {
            var resolvedSelection = VisibleUsers.FirstOrDefault(item => item.UserId == otherUserId)
                ?? updatedItem;
            ApplySelectedConversationState(resolvedSelection);
        }
    }

    private static ConversationMessageItem MapConversationMessage(MessageDto message, Guid currentUserId)
    {
        return new ConversationMessageItem
        {
            MessageId = message.Id,
            Content = message.Content,
            SentAtText = FormatDetailedTimestamp(message.SendTime),
            IsOwnMessage = message.SenderId == currentUserId,
            IsDeleteVisible = false
        };
    }

    private static string BuildMessagePreview(string? content)
    {
        var normalizedContent = content?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            return "Mesaj icerigi bulunamadi.";
        }

        return normalizedContent.Length <= 42
            ? normalizedContent
            : $"{normalizedContent[..39]}...";
    }

    private static string ResolveConversationSubtitle(MessageConversationUserItem user)
    {
        return user.LastActivityUtc.HasValue
            ? $"Son hareket: {FormatDetailedTimestamp(user.LastActivityUtc.Value)}"
            : "Bu kisi ile yeni bir sohbet baslatabilirsiniz.";
    }

    private static string FormatConversationListTime(DateTime value)
    {
        var localValue = ConvertToLocalTime(value);
        // DateTime.Now is correct here: comparing against already-localized time
        return localValue.Date == DateTime.Now.Date
            ? localValue.ToString("HH:mm")
            : localValue.ToString("dd.MM.yyyy");
    }

    private static string FormatDetailedTimestamp(DateTime value)
    {
        return ConvertToLocalTime(value).ToString("dd.MM.yyyy HH:mm");
    }

    private static DateTime ConvertToLocalTime(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value.ToLocalTime(),
            DateTimeKind.Local => value,
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime()
        };
    }
}

public partial class MessageConversationUserItem : ObservableObject
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Initials { get; init; } = string.Empty;
    public string SecondaryText { get; init; } = string.Empty;
    public string LastActivityText { get; init; } = string.Empty;
    public DateTime? LastActivityUtc { get; init; }

    [ObservableProperty]
    private bool isSelected;
}

public partial class ConversationMessageItem : ObservableObject
{
    public Guid MessageId { get; init; }
    public string Content { get; init; } = string.Empty;
    public string SentAtText { get; init; } = string.Empty;
    public bool IsOwnMessage { get; init; }

    [ObservableProperty]
    private bool isDeleteVisible;

    [ObservableProperty]
    private bool isDeleting;
}
