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
    private const int ConversationListPageSize = 100;
    private const int ConversationListMaxPages = 5;
    private const int SearchPageSize = 20;
    private const int MessagePageSize = 100;
    private const string EmptyConversationListMessage = "Henuz mesajlasilan kullanici yok.";
    private const string EmptySearchResultMessage = "Arama ile eslesen kullanici bulunamadi.";
    private const string EmptyConversationMessage = "";
    private const string NewConversationMessage = "Bu kullanici ile henuz mesaj bulunmuyor.";

    private readonly List<MessageConversationUserItem> priorConversationUsers = [];
    private readonly Dictionary<Guid, MessageConversationUserItem> priorConversationUserMap = [];
    private readonly Dictionary<Guid, string> companyUserNameMap = [];
    private readonly List<CompanyUserDto> allCompanyUsers = [];
    private readonly List<MessageDto> recentDirectMessages = [];

    private CancellationTokenSource? conversationLoadCancellationTokenSource;
    private CancellationTokenSource? searchCancellationTokenSource;

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

    [ObservableProperty]
    private bool isSearchingUsers;

    [ObservableProperty]
    private bool isConversationLoading;

    [ObservableProperty]
    private bool isSendingMessage;

    [ObservableProperty]
    private string usersEmptyMessage = EmptyConversationListMessage;

    [ObservableProperty]
    private string selectedConversationTitle = string.Empty;

    [ObservableProperty]
    private string selectedConversationSubtitle = string.Empty;

    [ObservableProperty]
    private string conversationEmptyMessage = string.Empty;

    [ObservableProperty]
    private bool hasSelectedConversation;

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
            await LoadWorkerReportAccessStateAsync();

            try
            {
                await signalRChatService.EnsureConnectedAsync();
            }
            catch
            {
            }

            var currentUserId = UserSession.UserId.Value;
            var companyId = UserSession.CompanyId.Value;
            var usersTask = LoadPriorConversationUsersAsync(currentUserId, companyId);
            var unreadTask = chatApiClient.GetUnreadMessageCountAsync(currentUserId);

            await Task.WhenAll(usersTask, unreadTask);

            ReplacePriorConversationUsers(await usersTask);
            UnreadCount = await unreadTask;
            StatusText = priorConversationUsers.Count > 0
                ? $"Son yazisilan kisi sayisi: {priorConversationUsers.Count}"
                : string.Empty;

            await RefreshVisibleUsersForCurrentSearchAsync();
            await RestoreSelectionAfterLoadAsync();
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
            ErrorMessage = "Bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsBusy = false;
        }
    }

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
                ReplaceConversationMessages(cachedMessages, currentUserId);
                return;
            }

            ConversationMessages.Clear();
            IsConversationLoading = true;

            var messages = await chatApiClient.GetMessagesBetweenUsersAsync(
                currentUserId,
                currentUserId,
                user.UserId,
                1,
                MessagePageSize,
                cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var conversationMessages = (messages ?? [])
                .Where(message => !message.IsDeleted && message.GroupId is null)
                .OrderBy(message => message.SendTime)
                .ToList();

            CacheConversationMessages(conversationMessages);
            ReplaceConversationMessages(conversationMessages, currentUserId);
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

        var content = MessageDraft.Trim();

        try
        {
            IsSendingMessage = true;
            ErrorMessage = string.Empty;

            await chatApiClient.CreateMessageCommandRequestAsync(new
            {
                Content = content,
                SenderId = currentUserId,
                ReceiverId = SelectedConversationUser.UserId,
                GroupId = (Guid?)null
            });

            var message = new MessageDto
            {
                Id = Guid.NewGuid(),
                Content = content,
                SendTime = DateTime.UtcNow,
                SenderId = currentUserId,
                ReceiverId = SelectedConversationUser.UserId,
                IsDelivered = true,
                IsRead = true
            };

            UpsertDirectMessageCache(message);
            ConversationMessages.Add(MapConversationMessage(message, currentUserId));
            ConversationEmptyMessage = string.Empty;
            MessageDraft = string.Empty;
            UpsertPriorConversationUser(message, SelectedConversationUser.UserId, SelectedConversationUser.Name);
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
        _ = RefreshVisibleUsersForCurrentSearchAsync();
    }

    private async Task<List<MessageConversationUserItem>> LoadPriorConversationUsersAsync(Guid currentUserId, Guid companyId)
    {
        var usersTask = identityApiClient.GetAllCompanyUsersAsync(companyId);
        var messages = await LoadRecentDirectMessagesAsync(currentUserId);
        var users = await usersTask ?? [];
        CacheRecentDirectMessages(messages);

        allCompanyUsers.Clear();
        allCompanyUsers.AddRange(users.Where(user => user.Id != Guid.Empty));
        companyUserNameMap.Clear();
        foreach (var user in users.Where(item => item.Id != Guid.Empty))
        {
            companyUserNameMap[user.Id] = ResolveDisplayName(user);
        }

        return messages
            .Select(message => new
            {
                Message = message,
                OtherUserId = ResolveOtherUserId(message, currentUserId)
            })
            .Where(item => item.OtherUserId.HasValue && item.OtherUserId.Value != Guid.Empty)
            .GroupBy(item => item.OtherUserId!.Value)
            .Select(group =>
            {
                var latestMessage = group
                    .OrderByDescending(item => item.Message.SendTime)
                    .First()
                    .Message;

                return BuildConversationUserItem(
                    group.Key,
                    ResolveDisplayName(group.Key),
                    latestMessage);
            })
            .OrderByDescending(item => item.LastActivityUtc ?? DateTime.MinValue)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<MessageDto>> LoadRecentDirectMessagesAsync(Guid currentUserId)
    {
        var messages = new List<MessageDto>();

        for (var page = 1; page <= ConversationListMaxPages; page++)
        {
            var pageItems = await chatApiClient.GetMessagesByUserIdAsync(currentUserId, page, ConversationListPageSize);
            var directMessages = (pageItems ?? [])
                .Where(message => !message.IsDeleted && message.GroupId is null)
                .ToList();

            if (directMessages.Count == 0)
            {
                break;
            }

            messages.AddRange(directMessages);

            if (directMessages.Count < ConversationListPageSize)
            {
                break;
            }
        }

        return messages;
    }

    private async Task RefreshVisibleUsersForCurrentSearchAsync()
    {
        var currentSearch = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(currentSearch))
        {
            searchCancellationTokenSource?.Cancel();
            searchCancellationTokenSource?.Dispose();
            searchCancellationTokenSource = null;
            IsSearchingUsers = false;
            ApplyVisibleUsers(priorConversationUsers);
            UsersEmptyMessage = EmptyConversationListMessage;
            return;
        }

        if (UserSession.CompanyId is not Guid companyId || UserSession.UserId is not Guid currentUserId)
        {
            return;
        }

        searchCancellationTokenSource?.Cancel();
        searchCancellationTokenSource?.Dispose();
        var currentCancellationSource = new CancellationTokenSource();
        searchCancellationTokenSource = currentCancellationSource;

        try
        {
            IsSearchingUsers = true;
            await Task.Delay(250, currentCancellationSource.Token);

            var result = await identityApiClient.SearchCompanyUsersAsync(
                companyId,
                currentSearch,
                1,
                SearchPageSize,
                currentCancellationSource.Token);

            if (!ReferenceEquals(searchCancellationTokenSource, currentCancellationSource) || currentCancellationSource.IsCancellationRequested)
            {
                return;
            }

            var items = (result?.Items ?? [])
                .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
                .Select(BuildSearchResultUser)
                .OrderByDescending(item => item.LastActivityUtc ?? DateTime.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyVisibleUsers(items);
            UsersEmptyMessage = EmptySearchResultMessage;
        }
        catch (OperationCanceledException)
        {
        }
        catch (ApiException)
        {
            await ApplySearchFallbackAsync(companyId, currentUserId, currentSearch, currentCancellationSource.Token);
        }
        catch (HttpRequestException)
        {
            await ApplySearchFallbackAsync(companyId, currentUserId, currentSearch, currentCancellationSource.Token);
        }
        catch (Exception)
        {
            await ApplySearchFallbackAsync(companyId, currentUserId, currentSearch, currentCancellationSource.Token);
        }
        finally
        {
            if (ReferenceEquals(searchCancellationTokenSource, currentCancellationSource))
            {
                searchCancellationTokenSource = null;
                IsSearchingUsers = false;
            }

            currentCancellationSource.Dispose();
        }
    }

    private async Task RestoreSelectionAfterLoadAsync()
    {
        if (SelectedConversationUser is not null)
        {
            var preservedSelection = VisibleUsers.FirstOrDefault(item => item.UserId == SelectedConversationUser.UserId)
                ?? priorConversationUsers.FirstOrDefault(item => item.UserId == SelectedConversationUser.UserId);

            if (preservedSelection is not null)
            {
                await SelectConversationUserAsync(preservedSelection);
                return;
            }
        }

        if (priorConversationUsers.Count > 0 && string.IsNullOrWhiteSpace(SearchText))
        {
            await SelectConversationUserAsync(priorConversationUsers[0]);
            return;
        }

        ApplySelectedConversationState(null);
        ConversationMessages.Clear();
    }

    private void ReplacePriorConversationUsers(IEnumerable<MessageConversationUserItem> users)
    {
        priorConversationUsers.Clear();
        priorConversationUserMap.Clear();

        foreach (var user in users)
        {
            priorConversationUsers.Add(user);
            priorConversationUserMap[user.UserId] = user;
        }
    }

    private void CacheRecentDirectMessages(IEnumerable<MessageDto> messages)
    {
        recentDirectMessages.Clear();

        foreach (var message in messages
            .Where(message => !message.IsDeleted && message.GroupId is null)
            .OrderByDescending(message => message.SendTime))
        {
            recentDirectMessages.Add(message);
        }
    }

    private void CacheConversationMessages(IEnumerable<MessageDto> messages)
    {
        foreach (var message in messages)
        {
            UpsertDirectMessageCache(message);
        }
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

    private void ApplyVisibleUsers(IEnumerable<MessageConversationUserItem> users)
    {
        var selectedUserId = SelectedConversationUser?.UserId;

        VisibleUsers.Clear();
        foreach (var user in users)
        {
            user.IsSelected = selectedUserId.HasValue && user.UserId == selectedUserId.Value;
            VisibleUsers.Add(user);
        }
    }

    private void ApplySelectedConversationState(MessageConversationUserItem? user)
    {
        SelectedConversationUser = user;
        HasSelectedConversation = user is not null;
        SelectedConversationTitle = user?.Name ?? string.Empty;
        SelectedConversationSubtitle = user is null ? string.Empty : ResolveConversationSubtitle(user);
        ConversationEmptyMessage = user is null ? string.Empty : NewConversationMessage;
        ApplySelectionState(user?.UserId);
    }

    private async Task ApplySearchFallbackAsync(
        Guid companyId,
        Guid currentUserId,
        string currentSearch,
        CancellationToken cancellationToken)
    {
        try
        {
            if (allCompanyUsers.Count == 0)
            {
                var users = await identityApiClient.GetAllCompanyUsersAsync(companyId, cancellationToken) ?? [];
                allCompanyUsers.Clear();
                allCompanyUsers.AddRange(users.Where(user => user.Id != Guid.Empty));

                foreach (var user in allCompanyUsers)
                {
                    companyUserNameMap[user.Id] = ResolveDisplayName(user);
                }
            }

            var items = allCompanyUsers
                .Where(user => user.Id != currentUserId)
                .Where(user => ResolveDisplayName(user).Contains(currentSearch, StringComparison.OrdinalIgnoreCase))
                .Select(BuildSearchResultUser)
                .OrderByDescending(item => item.LastActivityUtc ?? DateTime.MinValue)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            ApplyVisibleUsers(items);
            UsersEmptyMessage = EmptySearchResultMessage;
            ErrorMessage = string.Empty;
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            ErrorMessage = string.Empty;
        }
    }
    private void ApplySelectionState(Guid? selectedUserId)
    {
        foreach (var item in priorConversationUsers)
        {
            item.IsSelected = selectedUserId.HasValue && item.UserId == selectedUserId.Value;
        }

        foreach (var item in VisibleUsers)
        {
            item.IsSelected = selectedUserId.HasValue && item.UserId == selectedUserId.Value;
        }
    }

    private void UpsertPriorConversationUser(MessageDto message, Guid otherUserId, string? fallbackName = null)
    {
        var updatedItem = BuildConversationUserItem(otherUserId, ResolveDisplayName(otherUserId, fallbackName), message);
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

            UpsertDirectMessageCache(message);
            UpsertPriorConversationUser(message, otherUserId.Value);

            if (SelectedConversationUser?.UserId == otherUserId.Value)
            {
                ConversationMessages.Add(MapConversationMessage(message, currentUserId));
                ConversationEmptyMessage = string.Empty;
                return;
            }

            if (message.ReceiverId == currentUserId)
            {
                UnreadCount += 1;
            }
        });
    }

    private MessageConversationUserItem BuildSearchResultUser(CompanyUserDto user)
    {
        companyUserNameMap[user.Id] = ResolveDisplayName(user);

        if (priorConversationUserMap.TryGetValue(user.Id, out var existingItem))
        {
            return new MessageConversationUserItem
            {
                UserId = existingItem.UserId,
                Name = ResolveDisplayName(user),
                Initials = existingItem.Initials,
                SecondaryText = existingItem.SecondaryText,
                LastActivityText = existingItem.LastActivityText,
                LastActivityUtc = existingItem.LastActivityUtc
            };
        }

        return BuildConversationUserItem(user.Id, ResolveDisplayName(user), null);
    }

    private static MessageConversationUserItem BuildConversationUserItem(Guid userId, string name, MessageDto? latestMessage)
    {
        var resolvedName = string.IsNullOrWhiteSpace(name) ? "Kullanici" : name.Trim();
        return new MessageConversationUserItem
        {
            UserId = userId,
            Name = resolvedName,
            Initials = BuildInitials(resolvedName),
            SecondaryText = latestMessage is null
                ? "Kullanici sec ve yeni bir mesaj gonder."
                : BuildMessagePreview(latestMessage.Content),
            LastActivityText = latestMessage is null
                ? string.Empty
                : FormatConversationListTime(latestMessage.SendTime),
            LastActivityUtc = latestMessage?.SendTime
        };
    }

    private string ResolveDisplayName(Guid userId, string? fallbackName = null)
    {
        if (companyUserNameMap.TryGetValue(userId, out var userName) && !string.IsNullOrWhiteSpace(userName))
        {
            return userName;
        }

        if (!string.IsNullOrWhiteSpace(fallbackName))
        {
            return fallbackName.Trim();
        }

        return "Kullanici";
    }

    private static string ResolveDisplayName(CompanyUserDto user)
    {
        return string.IsNullOrWhiteSpace(user.Name)
            ? "Kullanici"
            : user.Name.Trim();
    }

    private static Guid? ResolveOtherUserId(MessageDto message, Guid currentUserId)
    {
        if (message.GroupId is not null)
        {
            return null;
        }

        if (message.SenderId == currentUserId)
        {
            return message.ReceiverId;
        }

        if (message.ReceiverId == currentUserId)
        {
            return message.SenderId;
        }

        return null;
    }

    private static ConversationMessageItem MapConversationMessage(MessageDto message, Guid currentUserId)
    {
        return new ConversationMessageItem
        {
            Content = message.Content,
            SentAtText = FormatDetailedTimestamp(message.SendTime),
            IsOwnMessage = message.SenderId == currentUserId
        };
    }

    private static string BuildInitials(string name)
    {
        var initials = string.Concat(name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0])));

        return string.IsNullOrWhiteSpace(initials) ? "K" : initials;
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

public sealed record ConversationMessageItem
{
    public string Content { get; init; } = string.Empty;
    public string SentAtText { get; init; } = string.Empty;
    public bool IsOwnMessage { get; init; }
}