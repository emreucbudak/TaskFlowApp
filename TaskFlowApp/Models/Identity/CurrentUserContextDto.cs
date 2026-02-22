namespace TaskFlowApp.Models.Identity;

public sealed record CurrentUserContextDto
{
    public Guid UserId { get; init; }
    public Guid CompanyId { get; init; }
    public string Role { get; init; } = string.Empty;
}
