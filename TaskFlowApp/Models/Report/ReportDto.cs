namespace TaskFlowApp.Models.Report;

public sealed record ReportDto
{
    public Guid Id { get; init; }
    public int ReportTopicId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Guid ReportingUserId { get; init; }
    public int ReportStatusId { get; init; }
    public Guid NotifiedDepartmantId { get; init; }
    public DateTime CreatedAt { get; init; }
}
