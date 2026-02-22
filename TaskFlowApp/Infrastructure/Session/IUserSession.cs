namespace TaskFlowApp.Infrastructure.Session;

public interface IUserSession
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    Guid? UserId { get; }
    Guid? CompanyId { get; }
    string? Role { get; }

    void SetRawTokens(string accessToken, string? refreshToken);
    void SetTokens(string accessToken, string? refreshToken, Guid? userIdOverride = null, Guid? companyIdOverride = null, string? roleOverride = null);
    void Clear();
}
