using System.Text;
using System.Text.Json;

namespace TaskFlowApp.Infrastructure.Session;

public sealed class UserSession : IUserSession
{
    private static readonly string[] UserIdClaimKeys =
    [
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
        "nameid",
        "sub"
    ];

    private static readonly string[] CompanyIdClaimKeys =
    [
        "companyId",
        "CompanyId",
        "tenantId",
        "TenantId"
    ];

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CompanyId { get; private set; }

    public void SetTokens(string accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = ReadGuidClaim(accessToken, UserIdClaimKeys);
        CompanyId = ReadGuidClaim(accessToken, CompanyIdClaimKeys);
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        CompanyId = null;
    }

    private static Guid? ReadGuidClaim(string jwtToken, IEnumerable<string> claimKeys)
    {
        try
        {
            var tokenParts = jwtToken.Split('.');
            if (tokenParts.Length < 2)
            {
                return null;
            }

            var payload = tokenParts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2:
                    payload += "==";
                    break;
                case 3:
                    payload += "=";
                    break;
            }

            var jsonBytes = Convert.FromBase64String(payload);
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(jsonBytes));
            var root = document.RootElement;

            foreach (var key in claimKeys)
            {
                if (!root.TryGetProperty(key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }
}
