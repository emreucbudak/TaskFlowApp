using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Identity;

namespace TaskFlowApp.ViewModels;

public partial class GroupDetailsPageViewModel
{
    [ObservableProperty]
    private bool isGroupEventFormVisible;

    [ObservableProperty]
    private string newEventSubject = string.Empty;

    [ObservableProperty]
    private string selectedEventType = "Duyuru";

    [ObservableProperty]
    private string newEventTitle = string.Empty;

    [ObservableProperty]
    private string newEventDescription = string.Empty;

    [ObservableProperty]
    private DateTime newEventStartDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan newEventStartTime = DateTime.Now.AddHours(1).TimeOfDay;

    [ObservableProperty]
    private bool isEventEndEnabled;

    [ObservableProperty]
    private DateTime newEventEndDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan newEventEndTime = DateTime.Now.AddHours(2).TimeOfDay;

    [ObservableProperty]
    private string newEventMeetingLink = string.Empty;

    [ObservableProperty]
    private bool isSubmittingGroupEvent;

    [ObservableProperty]
    private string groupEventsEmptyMessage = NoGroupEventsMessage;

    private async Task LoadGroupEventsAsync()
    {
        ResetGroupEventsState();

        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            var events = await identityApiClient.GetGroupEventsAsync(currentGroupId) ?? [];
            foreach (var groupEvent in events.OrderBy(e => e.StartsAt))
            {
                GroupEvents.Add(new GroupEventDisplayItem
                {
                    GroupEventId = groupEvent.GroupEventId,
                    Subject = groupEvent.Subject,
                    EventType = groupEvent.EventType,
                    Title = groupEvent.Title,
                    Description = groupEvent.Description,
                    StartsAtText = FormatDateTime(groupEvent.StartsAt),
                    EndsAtText = groupEvent.EndsAt.HasValue ? FormatDateTime(groupEvent.EndsAt.Value) : string.Empty,
                    MeetingLink = groupEvent.MeetingLink,
                    CreatedByUserName = groupEvent.CreatedByUserName,
                    CreatedAtText = FormatRelativeTime(groupEvent.CreatedAt),
                    HasEndsAt = groupEvent.EndsAt.HasValue,
                    CanDelete = IsGroupLeader,
                    HasMeetingLink = !string.IsNullOrWhiteSpace(groupEvent.MeetingLink)
                });
            }

            OnPropertyChanged(nameof(HasGroupEvents));
            OnPropertyChanged(nameof(HasNoGroupEvents));
            GroupEventsEmptyMessage = GroupEvents.Count > 0 ? string.Empty : NoGroupEventsMessage;
        }
        catch (Exception ex)
        {
            LogSilentFailure(ex);
            ResetGroupEventsState();
        }
    }

    [RelayCommand]
    private void ShowGroupEventForm()
    {
        IsGroupEventFormVisible = true;
        NewEventSubject = string.Empty;
        SelectedEventType = EventTypeOptions.FirstOrDefault() ?? "Duyuru";
        NewEventTitle = string.Empty;
        NewEventDescription = string.Empty;
        NewEventStartDate = DateTime.Today;
        NewEventStartTime = DateTime.Now.AddHours(1).TimeOfDay;
        IsEventEndEnabled = false;
        NewEventEndDate = DateTime.Today;
        NewEventEndTime = DateTime.Now.AddHours(2).TimeOfDay;
        NewEventMeetingLink = string.Empty;
    }

    [RelayCommand]
    private void HideGroupEventForm()
    {
        IsGroupEventFormVisible = false;
        NewEventSubject = string.Empty;
        SelectedEventType = EventTypeOptions.FirstOrDefault() ?? "Duyuru";
        NewEventTitle = string.Empty;
        NewEventDescription = string.Empty;
        NewEventStartDate = DateTime.Today;
        NewEventStartTime = DateTime.Now.AddHours(1).TimeOfDay;
        IsEventEndEnabled = false;
        NewEventEndDate = DateTime.Today;
        NewEventEndTime = DateTime.Now.AddHours(2).TimeOfDay;
        NewEventMeetingLink = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitGroupEventAsync()
    {
        if (!IsGroupLeader)
        {
            ErrorMessage = "Etkinlik oluşturmak için grup lideri olmalısınız.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEventSubject))
        {
            ErrorMessage = "Etkinlik konusu boş olamaz.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedEventType))
        {
            ErrorMessage = "Etkinlik türü boş olamaz.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewEventTitle))
        {
            ErrorMessage = "Etkinlik başlığı boş olamaz.";
            return;
        }

        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        var startsAtLocal = CombineDateAndTime(NewEventStartDate, NewEventStartTime);
        DateTime? endsAtLocal = null;

        if (IsEventEndEnabled)
        {
            endsAtLocal = CombineDateAndTime(NewEventEndDate, NewEventEndTime);
            if (endsAtLocal <= startsAtLocal)
            {
                ErrorMessage = "Bitiş zamanı başlangıç zamanından sonra olmalı.";
                return;
            }
        }

        var meetingLink = NewEventMeetingLink?.Trim();
        if (!string.IsNullOrWhiteSpace(meetingLink) && !Uri.TryCreate(meetingLink, UriKind.Absolute, out _))
        {
            ErrorMessage = "Geçerli bir toplantı bağlantısı girin.";
            return;
        }

        try
        {
            IsSubmittingGroupEvent = true;
            ErrorMessage = string.Empty;

            await identityApiClient.CreateGroupEventAsync(new CreateGroupEventRequestDto
            {
                GroupId = currentGroupId,
                Subject = NewEventSubject.Trim(),
                EventType = SelectedEventType.Trim(),
                Title = NewEventTitle.Trim(),
                Description = NewEventDescription?.Trim() ?? string.Empty,
                StartsAt = DateTime.SpecifyKind(startsAtLocal, DateTimeKind.Local).ToUniversalTime(),
                EndsAt = endsAtLocal.HasValue
                    ? DateTime.SpecifyKind(endsAtLocal.Value, DateTimeKind.Local).ToUniversalTime()
                    : null,
                MeetingLink = string.IsNullOrWhiteSpace(meetingLink) ? null : meetingLink
            });

            HideGroupEventForm();
            await LoadGroupEventsAsync();
            StatusText = "Etkinlik yayımlandı.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Etkinlik oluşturulamadı.");
        }
        catch (Exception)
        {
            ErrorMessage = "Etkinlik oluşturulurken bir hata oluştu.";
        }
        finally
        {
            IsSubmittingGroupEvent = false;
        }
    }

    [RelayCommand]
    private async Task DeleteGroupEventAsync(Guid groupEventId)
    {
        if (groupEventId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;

            await identityApiClient.DeleteGroupEventAsync(groupEventId);
            await LoadGroupEventsAsync();
            StatusText = "Etkinlik silindi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Etkinlik silinemedi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Etkinlik silinirken bir hata oluştu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenMeetingLinkAsync(string? meetingLink)
    {
        if (string.IsNullOrWhiteSpace(meetingLink))
        {
            return;
        }

        if (!Uri.TryCreate(meetingLink.Trim(), UriKind.Absolute, out var uri))
        {
            ErrorMessage = "Geçerli bir toplantı bağlantısı bulunamadı.";
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(uri);
        }
        catch
        {
            ErrorMessage = "Toplantı bağlantısı açılamadı.";
        }
    }

    private void ResetGroupEventsState()
    {
        GroupEvents.Clear();
        GroupEventsEmptyMessage = NoGroupEventsMessage;
        OnPropertyChanged(nameof(HasGroupEvents));
        OnPropertyChanged(nameof(HasNoGroupEvents));
    }

    private static DateTime CombineDateAndTime(DateTime date, TimeSpan time)
    {
        return date.Date.Add(time);
    }
}

public sealed record GroupEventDisplayItem
{
    public Guid GroupEventId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StartsAtText { get; init; } = string.Empty;
    public string EndsAtText { get; init; } = string.Empty;
    public string? MeetingLink { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public string CreatedAtText { get; init; } = string.Empty;
    public bool CanDelete { get; init; }
    public bool HasMeetingLink { get; init; }
    public bool HasEndsAt { get; init; }
}
