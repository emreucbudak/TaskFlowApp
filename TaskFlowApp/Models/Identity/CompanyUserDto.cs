namespace TaskFlowApp.Models.Identity;

public sealed record CompanyUserDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
