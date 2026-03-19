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
using TaskFlowApp.Services.State;
using TaskFlowApp.Infrastructure.Authorization;
using TaskFlowApp.Infrastructure.Constants;

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient,
    ISignalRChatService signalRChatService,
    IWorkerReportAccessResolver workerReportAccessResolver,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager, workerReportAccessResolver, workerDashboardStateService)
{
    private const int ConversationListPageSize = 100;
    private const int ConversationListMaxPages = 5;
    private const int SearchPageSize = 20;
    private const int MessagePageSize = 100;
    private const int DepartmentLeaderRoleId = 1;
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

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private MessageConversationUserItem? selectedConversationUser;

    [ObservableProperty]
    private int unreadCount;

    [ObservableProperty]
    private bool isSearchingUsers;

    [ObservableProperty]
    private string usersEmptyMessage = EmptyConversationListMessage;

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
            ConfigureDepartmentGroupState(currentUserId);
            UnreadCount = await unreadTask;
            workerDashboardStateService.SetUnreadMessageCount(UnreadCount);
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

    partial void OnSearchTextChanged(string value)
    {
        _ = RefreshVisibleUsersForCurrentSearchAsync();
    }

    public void RegisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
        signalRChatService.PrivateMessageReceived += OnPrivateMessageReceived;
    }

    public void UnregisterRealtimeHandlers()
    {
        signalRChatService.PrivateMessageReceived -= OnPrivateMessageReceived;
    }

    [RelayCommand]
    private Task DisconnectRealtimeAsync() => signalRChatService.DisconnectAsync();

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

    private void ApplySelectedConversationState(MessageConversationUserItem? user)
    {
        SelectedConversationUser = user;
        HasSelectedConversation = user is not null;
        SelectedConversationTitle = user?.Name ?? string.Empty;
        SelectedConversationSubtitle = user is null ? string.Empty : ResolveConversationSubtitle(user);
        ConversationEmptyMessage = user is null ? EmptyConversationMessage : NewConversationMessage;
        ApplySelectionState(user?.UserId);
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
            Initials = BuildInitials(resolvedName, "K"),
            SecondaryText = latestMessage is null
                ? "Kullanici sec ve yeni bir mesaj gonder."
                : BuildMessagePreview(latestMessage.Content),
            LastActivityText = latestMessage is null
                ? string.Empty
                : FormatConversationListTime(latestMessage.SendTime),
            LastActivityUtc = latestMessage?.SendTime
        };
    }
}
