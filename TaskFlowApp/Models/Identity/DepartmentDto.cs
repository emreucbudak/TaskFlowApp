namespace TaskFlowApp.Models.Identity;

public sealed record DepartmentDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
