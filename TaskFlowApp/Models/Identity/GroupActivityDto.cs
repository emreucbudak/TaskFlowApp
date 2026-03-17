namespace TaskFlowApp.Models.Identity;

public sealed record GroupActivityDto
{
    public Guid ActivityId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid SubmittedByUserId { get; init; }
    public string SubmittedByUserName { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public int Status { get; init; }
    public string StatusText { get; init; } = string.Empty;
    public Guid? ReviewedByUserId { get; init; }
    public string? ReviewedByUserName { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewNote { get; init; }
}
