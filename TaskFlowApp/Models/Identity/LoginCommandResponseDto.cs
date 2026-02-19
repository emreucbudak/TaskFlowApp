namespace TaskFlowApp.Models.Identity;

public sealed record LoginCommandResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}
