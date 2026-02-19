namespace TaskFlowApp.Infrastructure.Session;

public interface IUserSession
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    Guid? UserId { get; }
    Guid? CompanyId { get; }

    void SetTokens(string accessToken, string? refreshToken);
    void Clear();
}
