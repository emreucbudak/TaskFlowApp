namespace TaskFlowApp.Models.ProjectManagement;

public sealed record CompanyTaskDto
{
    public string TaskName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateOnly DeadlineTime { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
}
