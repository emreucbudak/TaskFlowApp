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
        "companyid",
        "company_id",
        "tenantId",
        "TenantId",
        "tenantid",
        "tenant_id",
        "sid",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/sid",
        "http://schemas.microsoft.com/identity/claims/tenantid"
    ];

    private static readonly string[] RoleClaimKeys =
    [
        "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        "role",
        "Role",
        "roles"
    ];

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? Role { get; private set; }

    public void SetRawTokens(string accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = null;
        CompanyId = null;
        Role = null;
    }

    public void SetTokens(
        string accessToken,
        string? refreshToken,
        Guid? userIdOverride = null,
        Guid? companyIdOverride = null,
        string? roleOverride = null)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = userIdOverride ?? ReadGuidClaim(accessToken, UserIdClaimKeys);
        CompanyId = companyIdOverride ?? ReadGuidClaim(accessToken, CompanyIdClaimKeys);
        Role = string.IsNullOrWhiteSpace(roleOverride)
            ? ReadRoleClaim(accessToken, RoleClaimKeys)
            : roleOverride;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        CompanyId = null;
        Role = null;
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
                if (!TryGetPropertyIgnoreCase(root, key, out var value))
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

    private static string? ReadRoleClaim(string jwtToken, IEnumerable<string> claimKeys)
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
                if (!TryGetPropertyIgnoreCase(root, key, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String)
                {
                    var role = value.GetString();
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        return role;
                    }
                }

                if (value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in value.EnumerateArray())
                    {
                        if (element.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }

                        var role = element.GetString();
                        if (!string.IsNullOrWhiteSpace(role))
                        {
                            return role;
                        }
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement root, string key, out JsonElement value)
    {
        if (root.TryGetProperty(key, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
