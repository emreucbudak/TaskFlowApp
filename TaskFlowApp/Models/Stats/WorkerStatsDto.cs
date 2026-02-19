namespace TaskFlowApp.Models.Stats;

public sealed record WorkerStatsDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateOnly Period { get; init; }
    public int TotalTasksAssigned { get; init; }
    public int TotalTasksCompleted { get; init; }
    public int TasksCompletedBeforeDeadline { get; init; }
    public int OverdueIncompleteTasksCount { get; init; }
}
