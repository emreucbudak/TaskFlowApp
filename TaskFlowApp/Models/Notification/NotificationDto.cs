namespace TaskFlowApp.Models.Notification;

public sealed record NotificationDto
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime SendTime { get; init; }
    public bool IsRead { get; init; }
}
