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

namespace TaskFlowApp.ViewModels;

public partial class MessagesPageViewModel(
    INavigationService navigationService,
    IUserSession userSession,
    IRealtimeConnectionManager realtimeConnectionManager,
    ChatApiClient chatApiClient,
    IdentityApiClient identityApiClient,
    ISignalRChatService signalRChatService,
    IWorkerDashboardStateService workerDashboardStateService) : PageViewModelBase(navigationService, userSession, realtimeConnectionManager)
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
    public ObservableCollection<ConversationMessageItem> ConversationMessages { get; } = [];
    public ObservableCollection<LeaderDepartmentOption> ManagedDepartments { get; } = [];
    public ObservableCollection<SelectableDepartmentUserItem> EligibleDepartmentUsers { get; } = [];

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
    private string conversationStatusMessage = string.Empty;

    [ObservableProperty]
    private bool hasSelectedConversation;

    [ObservableProperty]
    private bool isDepartmentLeader;

    [ObservableProperty]
    private bool isCreatingDepartmentGroup;

    [ObservableProperty]
    private string groupNameInput = string.Empty;

    [ObservableProperty]
    private string groupCreationStatus = string.Empty;

    [ObservableProperty]
    private LeaderDepartmentOption? selectedManagedDepartment;

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

    [RelayCommand]
    private async Task CreateDepartmentGroupAsync()
    {
        if (IsCreatingDepartmentGroup || !IsDepartmentLeader)
        {
            return;
        }

        if (UserSession.CompanyId is not Guid companyId)
        {
            ErrorMessage = "Sirket bilgisi bulunamadi. Tekrar giris yapin.";
            return;
        }

        if (SelectedManagedDepartment is null)
        {
            ErrorMessage = "Grup icin yonettiginiz departmani secin.";
            return;
        }

        var trimmedGroupName = GroupNameInput.Trim();
        if (trimmedGroupName.Length < 3 || trimmedGroupName.Length > 100)
        {
            ErrorMessage = "Grup adi 3 ile 100 karakter arasinda olmalidir.";
            return;
        }

        var selectedUserIds = EligibleDepartmentUsers
            .Where(item => item.IsSelected)
            .Select(item => item.UserId)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();

        if (selectedUserIds.Count == 0)
        {
            ErrorMessage = "Gruba eklemek icin en az bir calisan secin.";
            return;
        }

        try
        {
            IsCreatingDepartmentGroup = true;
            ErrorMessage = string.Empty;
            GroupCreationStatus = string.Empty;

            await identityApiClient.AddGroupsCommandRequestAsync(new
            {
                Name = trimmedGroupName,
                CompanyId = companyId,
                DepartmentId = SelectedManagedDepartment.DepartmentId,
                UserIds = selectedUserIds
            });

            foreach (var item in EligibleDepartmentUsers)
            {
                item.IsSelected = false;
            }

            GroupNameInput = string.Empty;
            GroupCreationStatus = "Departman grubu basariyla olusturuldu.";
            StatusText = $"{trimmedGroupName} grubu olusturuldu. Secilen calisan sayisi: {selectedUserIds.Count}";
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            ErrorMessage = "Grup olusturulamadi. Grup adi ve secilen kullanicilari kontrol edin.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Grup olusturulamadi. Lutfen tekrar deneyin.");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = GenericConnectionErrorMessage;
        }
        catch (Exception)
        {
            ErrorMessage = "Grup olusturulurken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsCreatingDepartmentGroup = false;
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

    partial void OnSelectedManagedDepartmentChanged(LeaderDepartmentOption? value)
    {
        GroupCreationStatus = string.Empty;
        RefreshEligibleDepartmentUsers();
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

    private void ConfigureDepartmentGroupState(Guid currentUserId)
    {
        ManagedDepartments.Clear();
        EligibleDepartmentUsers.Clear();
        IsDepartmentLeader = false;
        GroupCreationStatus = string.Empty;
        SelectedManagedDepartment = null;

        if (!string.Equals(UserSession.Role, "worker", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var currentUser = allCompanyUsers.FirstOrDefault(user => user.Id == currentUserId);
        if (currentUser is null)
        {
            return;
        }

        var managedDepartments = currentUser.DepartmentMemberships
            .Where(membership => membership.DepartmentId != Guid.Empty && membership.DepartmentRoleId == DepartmentLeaderRoleId)
            .Select(membership => new LeaderDepartmentOption
            {
                DepartmentId = membership.DepartmentId,
                DepartmentName = string.IsNullOrWhiteSpace(membership.DepartmentName)
                    ? "Departman"
                    : membership.DepartmentName.Trim()
            })
            .DistinctBy(item => item.DepartmentId)
            .OrderBy(item => item.DepartmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (managedDepartments.Count == 0)
        {
            return;
        }

        foreach (var department in managedDepartments)
        {
            ManagedDepartments.Add(department);
        }

        IsDepartmentLeader = true;
        SelectedManagedDepartment = ManagedDepartments[0];
        RefreshEligibleDepartmentUsers();
    }

    private void RefreshEligibleDepartmentUsers()
    {
        var selectedIds = EligibleDepartmentUsers
            .Where(item => item.IsSelected)
            .Select(item => item.UserId)
            .ToHashSet();

        EligibleDepartmentUsers.Clear();

        if (SelectedManagedDepartment is null || UserSession.UserId is not Guid currentUserId)
        {
            return;
        }

        var selectedDepartmentId = SelectedManagedDepartment.DepartmentId;
        var selectedDepartmentName = SelectedManagedDepartment.DepartmentName;

        var eligibleUsers = allCompanyUsers
            .Where(user => user.Id != Guid.Empty && user.Id != currentUserId)
            .Where(user => user.DepartmentMemberships.Any(membership => membership.DepartmentId == selectedDepartmentId))
            .OrderBy(user => ResolveDisplayName(user), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var user in eligibleUsers)
        {
            EligibleDepartmentUsers.Add(new SelectableDepartmentUserItem
            {
                UserId = user.Id,
                Name = ResolveDisplayName(user),
                DepartmentName = selectedDepartmentName,
                IsSelected = selectedIds.Contains(user.Id)
            });
        }
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
        ConversationEmptyMessage = user is null ? EmptyConversationMessage : NewConversationMessage;
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
            MessageId = message.Id,
            Content = message.Content,
            SentAtText = FormatDetailedTimestamp(message.SendTime),
            IsOwnMessage = message.SenderId == currentUserId,
            IsDeleteVisible = false
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

public sealed record LeaderDepartmentOption
{
    public Guid DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
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

public partial class SelectableDepartmentUserItem : ObservableObject
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DepartmentName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool isSelected;
}
