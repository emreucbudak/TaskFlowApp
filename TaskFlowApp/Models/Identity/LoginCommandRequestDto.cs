namespace TaskFlowApp.Models.Identity;

public sealed record LoginCommandRequestDto
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}
