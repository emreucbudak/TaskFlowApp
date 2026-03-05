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
    private const int PageSize = 20;
    private const int MaxNotificationWindowSize = 100;

    private int _nextPageToLoad = 1;
    private int _loadedFromApiCount;
    private int _totalCount;

    public ObservableCollection<NotificationDto> Notifications { get; } = [];

    [ObservableProperty]
    private bool isLoadingMore;

    [ObservableProperty]
    private bool hasMoreItems;

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

        var userId = UserSession.UserId.Value;

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            ResetPagination();
            Notifications.Clear();

            try
            {
                await signalRNotificationService.EnsureConnectedAsync();
            }
            catch
            {
                // SignalR baglantisi gecici olarak kurulamasa da bildirim listesi API'den yuklenmeye devam eder.
            }

            await LoadNextPageAsync(userId);
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

        await MarkLoadedUnreadNotificationsAsReadAsync(userId);
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsBusy || IsLoadingMore || !HasMoreItems)
        {
            return;
        }

        if (UserSession.UserId is null)
        {
            return;
        }

        try
        {
            IsLoadingMore = true;
            await LoadNextPageAsync(UserSession.UserId.Value);
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
            ErrorMessage = "Bildirimler yuklenirken bir sorun olustu. Lutfen tekrar deneyin.";
        }
        finally
        {
            IsLoadingMore = false;
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

    private async Task LoadNextPageAsync(Guid userId)
    {
        if (!HasMoreItems && _nextPageToLoad > 1)
        {
            return;
        }

        var response = await notificationApiClient.GetUserAllNotificationsAsync(userId, _nextPageToLoad, PageSize);
        var items = response?.Items ?? [];

        if (_nextPageToLoad == 1)
        {
            Notifications.Clear();
        }

        foreach (var item in items)
        {
            Notifications.Add(item);
        }

        _loadedFromApiCount += items.Count;
        _totalCount = Math.Min(response?.TotalCount ?? _loadedFromApiCount, MaxNotificationWindowSize);

        var canLoadMore = items.Count > 0
            && _loadedFromApiCount < _totalCount
            && _loadedFromApiCount < MaxNotificationWindowSize;

        HasMoreItems = canLoadMore;

        if (canLoadMore)
        {
            _nextPageToLoad++;
        }
    }

    private async Task MarkLoadedUnreadNotificationsAsReadAsync(Guid userId)
    {
        var unreadIndexes = Notifications
            .Select((notification, index) => new { notification, index })
            .Where(x => !x.notification.IsRead)
            .Select(x => x.index)
            .ToArray();

        if (unreadIndexes.Length == 0)
        {
            return;
        }

        try
        {
            // Ilk acilista okunmamis gorunum bir kare gosterilsin, sonra toplu okunmus isaretlensin.
            await Task.Yield();

            await notificationApiClient.MarkUserNotificationsAsReadAsync(userId, MaxNotificationWindowSize);

            foreach (var index in unreadIndexes)
            {
                var item = Notifications[index];
                Notifications[index] = item with { IsRead = true };
            }
        }
        catch
        {
            // Okunmus isaretleme cagrisi basarisiz olursa liste gosterimi kesilmez.
        }
    }

    private void OnNotificationReceived(NotificationDto item)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Notifications.Insert(0, item);

            if (Notifications.Count > MaxNotificationWindowSize)
            {
                Notifications.RemoveAt(Notifications.Count - 1);
            }
        });
    }

    private void ResetPagination()
    {
        _nextPageToLoad = 1;
        _loadedFromApiCount = 0;
        _totalCount = 0;
        HasMoreItems = true;
    }
}
