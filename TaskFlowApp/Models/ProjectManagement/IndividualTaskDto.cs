namespace TaskFlowApp.Models.ProjectManagement;

public sealed record IndividualTaskDto
{
    public Guid Id { get; init; }
    public Guid AssignedUserId { get; init; }
    public string TaskTitle { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly Deadline { get; init; }
}
