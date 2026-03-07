namespace TaskFlowApp.Models.ProjectManagement;

public sealed record CompanyTaskDto
{
    public string TaskName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly DeadlineTime { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string TaskPriorityName { get; init; } = string.Empty;
    public IReadOnlyList<CompanyTaskSubTaskDto> SubTasks { get; init; } = Array.Empty<CompanyTaskSubTaskDto>();
}

public sealed record CompanyTaskSubTaskDto
{
    public string TaskTitle { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid AssignedUserId { get; init; }
    public string StatusName { get; init; } = string.Empty;
}