namespace TaskFlowApp.Models.Chat;

public sealed record GroupRecentActivityItem
{
    public string ActorName { get; init; } = string.Empty;
    public string ActionText { get; init; } = string.Empty;
    public string OccurredAtText { get; init; } = string.Empty;
}
