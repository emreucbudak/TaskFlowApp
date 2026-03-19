using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Chat;

namespace TaskFlowApp.ViewModels;

public partial class GroupDetailsPageViewModel
{
    [ObservableProperty]
    private bool isActivityFormVisible;

    [ObservableProperty]
    private string newActivityTitle = string.Empty;

    [ObservableProperty]
    private string newActivityDescription = string.Empty;

    [ObservableProperty]
    private bool isSubmittingActivity;

    [ObservableProperty]
    private string recentGroupActivityEmptyMessage = NoRecentActivityMessage;

    private async Task LoadActivitiesAsync()
    {
        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            var activities = await identityApiClient.GetGroupActivitiesAsync(currentGroupId);
            Activities.Clear();

            if (activities is null || activities.Count == 0)
            {
                HasActivities = false;
                return;
            }

            foreach (var activity in activities)
            {
                Activities.Add(new GroupActivityDisplayItem
                {
                    ActivityId = activity.ActivityId,
                    Title = activity.Title,
                    Description = activity.Description,
                    SubmittedByUserName = activity.SubmittedByUserName,
                    SubmittedAtText = activity.SubmittedAt.ToString("dd.MM.yyyy HH:mm"),
                    Status = activity.Status,
                    StatusText = activity.StatusText,
                    StatusColor = activity.Status switch
                    {
                        0 => "#F59E0B",
                        1 => "#10B981",
                        2 => "#EF4444",
                        _ => "#64748B"
                    },
                    ReviewedByUserName = activity.ReviewedByUserName,
                    ReviewNote = activity.ReviewNote,
                    CanReview = IsGroupLeader && activity.Status == 0,
                    Initials = BuildInitials(activity.SubmittedByUserName, "U")
                });
            }

            HasActivities = Activities.Count > 0;
        }
        catch
        {
            HasActivities = false;
        }
    }

    private async Task LoadRecentGroupActivitiesAsync(Guid userId, IReadOnlyDictionary<Guid, string> userNameMap)
    {
        ResetRecentGroupActivitiesState();

        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            var messages = await chatApiClient.GetMessagesByGroupIdQueryRequestAsync(new
            {
                CurrentUserId = userId,
                GroupId = currentGroupId,
                PageSize = RecentActivityPageSize,
                Page = 1
            }) ?? [];

            var recentActivities = messages
                .Where(message => !message.IsDeleted)
                .OrderByDescending(message => message.SendTime)
                .Take(RecentActivityPreviewCount)
                .Select(message => new GroupRecentActivityItem
                {
                    ActorName = ResolveDisplayName(message.SenderId, userNameMap),
                    ActionText = ResolveRecentActivityText(message.Content),
                    OccurredAtText = FormatRelativeTime(message.SendTime)
                })
                .ToList();

            foreach (var activity in recentActivities)
            {
                RecentGroupActivities.Add(activity);
            }

            HasRecentGroupActivities = RecentGroupActivities.Count > 0;
            RecentGroupActivityEmptyMessage = HasRecentGroupActivities ? string.Empty : NoRecentActivityMessage;
        }
        catch
        {
            ResetRecentGroupActivitiesState();
        }
    }

    private void ResetRecentGroupActivitiesState()
    {
        RecentGroupActivities.Clear();
        HasRecentGroupActivities = false;
        RecentGroupActivityEmptyMessage = NoRecentActivityMessage;
    }

    private static string ResolveRecentActivityText(string? content)
    {
        var normalizedContent = content?.Trim();
        return string.IsNullOrWhiteSpace(normalizedContent)
            ? "gruba bir mesaj paylasti."
            : $"mesaj paylasti: {normalizedContent}";
    }

    [RelayCommand]
    private void ShowActivityForm()
    {
        IsActivityFormVisible = true;
        NewActivityTitle = string.Empty;
        NewActivityDescription = string.Empty;
    }

    [RelayCommand]
    private void HideActivityForm()
    {
        IsActivityFormVisible = false;
        NewActivityTitle = string.Empty;
        NewActivityDescription = string.Empty;
    }

    [RelayCommand]
    private async Task SubmitActivityAsync()
    {
        if (string.IsNullOrWhiteSpace(NewActivityTitle))
        {
            ErrorMessage = "Aktivite basligi bos olamaz.";
            return;
        }

        if (currentGroupId == Guid.Empty)
        {
            return;
        }

        try
        {
            IsSubmittingActivity = true;
            ErrorMessage = string.Empty;

            await identityApiClient.SubmitGroupActivityAsync(
                currentGroupId,
                NewActivityTitle.Trim(),
                NewActivityDescription?.Trim() ?? string.Empty);

            HideActivityForm();
            await LoadActivitiesAsync();
            StatusText = "Aktivite basariyla gonderildi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite gonderilemedi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Aktivite gonderilirken bir hata olustu.";
        }
        finally
        {
            IsSubmittingActivity = false;
        }
    }

    [RelayCommand]
    private async Task ApproveActivityAsync(Guid activityId)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await identityApiClient.ApproveGroupActivityAsync(activityId);
            await LoadActivitiesAsync();
            StatusText = "Aktivite onaylandi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite onaylanamadi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Onaylama sirasinda bir hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RejectActivityAsync(Guid activityId)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await identityApiClient.RejectGroupActivityAsync(activityId);
            await LoadActivitiesAsync();
            StatusText = "Aktivite reddedildi.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ResolveApiErrorMessage(ex, "Aktivite reddedilemedi.");
        }
        catch (Exception)
        {
            ErrorMessage = "Reddetme sirasinda bir hata olustu.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed record GroupActivityDisplayItem
{
    public Guid ActivityId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SubmittedByUserName { get; init; } = string.Empty;
    public string SubmittedAtText { get; init; } = string.Empty;
    public int Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public string StatusColor { get; init; } = "#64748B";
    public string? ReviewedByUserName { get; init; }
    public string? ReviewNote { get; init; }
    public bool CanReview { get; init; }
    public string Initials { get; init; } = string.Empty;
    public bool HasReviewNote => !string.IsNullOrWhiteSpace(ReviewNote);
    public string ReviewNoteDisplay => HasReviewNote ? $"Not: {ReviewNote}" : string.Empty;
}
