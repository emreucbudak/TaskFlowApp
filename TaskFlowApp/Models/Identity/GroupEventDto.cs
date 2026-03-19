namespace TaskFlowApp.Models.Identity;

public sealed record GroupEventDto
{
    public Guid GroupEventId { get; init; }
    public string Subject { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime StartsAt { get; init; }
    public DateTime? EndsAt { get; init; }
    public string? MeetingLink { get; init; }
    public Guid CreatedByUserId { get; init; }
    public string CreatedByUserName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
