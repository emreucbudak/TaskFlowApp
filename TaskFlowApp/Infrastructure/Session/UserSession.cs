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

    private static readonly string[] DisplayNameClaimKeys =
    [
        "name",
        "unique_name",
        "preferred_username",
        "given_name"
    ];

    private static readonly string[] EmailClaimKeys =
    [
        "email",
        "preferred_username",
        "upn",
        "unique_name"
    ];

    private static readonly string[] DepartmentClaimKeys =
    [
        "department",
        "Department",
        "departments",
        "Departments",
        "departmentName",
        "DepartmentName",
        "departmentNames",
        "DepartmentNames",
        "department_name",
        "department_names",
        "dept",
        "deptName",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/department"
    ];

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid? CompanyId { get; private set; }
    public string? Role { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Email { get; private set; }
    public IReadOnlyList<string> DepartmentNames { get; private set; } = Array.Empty<string>();

    public void SetRawTokens(string accessToken, string? refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        UserId = null;
        CompanyId = null;
        Role = null;
        DisplayName = ReadStringClaim(accessToken, DisplayNameClaimKeys);
        Email = ReadStringClaim(accessToken, EmailClaimKeys);
        DepartmentNames = ReadDepartmentClaims(accessToken);
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
        DisplayName = ReadStringClaim(accessToken, DisplayNameClaimKeys);
        Email = ReadStringClaim(accessToken, EmailClaimKeys);
        DepartmentNames = ReadDepartmentClaims(accessToken);
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        UserId = null;
        CompanyId = null;
        Role = null;
        DisplayName = null;
        Email = null;
        DepartmentNames = Array.Empty<string>();
    }

    private static Guid? ReadGuidClaim(string jwtToken, IEnumerable<string> claimKeys)
    {
        var stringValue = ReadStringClaim(jwtToken, claimKeys);
        return Guid.TryParse(stringValue, out var parsed) ? parsed : null;
    }

    private static string? ReadRoleClaim(string jwtToken, IEnumerable<string> claimKeys)
    {
        return ReadClaimValues(jwtToken, claimKeys).FirstOrDefault();
    }

    private static string? ReadStringClaim(string jwtToken, IEnumerable<string> claimKeys)
    {
        return ReadClaimValues(jwtToken, claimKeys).FirstOrDefault();
    }

    private static IReadOnlyList<string> ReadDepartmentClaims(string jwtToken)
    {
        return ReadClaimValues(jwtToken, DepartmentClaimKeys, splitCompositeValues: true);
    }

    private static IReadOnlyList<string> ReadClaimValues(
        string jwtToken,
        IEnumerable<string> claimKeys,
        bool splitCompositeValues = false)
    {
        try
        {
            using var document = ParseTokenPayload(jwtToken);
            if (document is null)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>();
            var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in claimKeys)
            {
                if (!TryGetPropertyIgnoreCase(document.RootElement, key, out var value))
                {
                    continue;
                }

                AppendClaimValues(value, values, seenValues, splitCompositeValues);
            }

            return values;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static JsonDocument? ParseTokenPayload(string jwtToken)
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
        return JsonDocument.Parse(Encoding.UTF8.GetString(jsonBytes));
    }

    private static void AppendClaimValues(
        JsonElement value,
        ICollection<string> values,
        ISet<string> seenValues,
        bool splitCompositeValues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                AppendStringValue(value.GetString(), values, seenValues, splitCompositeValues);
                break;
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    AppendClaimValues(item, values, seenValues, splitCompositeValues);
                }
                break;
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    if (splitCompositeValues &&
                        !property.Name.Contains("name", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(property.Name, "department", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(property.Name, "departments", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AppendClaimValues(property.Value, values, seenValues, splitCompositeValues);
                }
                break;
        }
    }

    private static void AppendStringValue(
        string? rawValue,
        ICollection<string> values,
        ISet<string> seenValues,
        bool splitCompositeValues)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return;
        }

        var trimmedValue = rawValue.Trim();
        if (TryAppendJsonArrayString(trimmedValue, values, seenValues, splitCompositeValues))
        {
            return;
        }

        if (!splitCompositeValues)
        {
            AddUniqueValue(trimmedValue, values, seenValues);
            return;
        }

        var parts = trimmedValue.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length <= 1)
        {
            AddUniqueValue(trimmedValue, values, seenValues);
            return;
        }

        foreach (var part in parts)
        {
            AddUniqueValue(part, values, seenValues);
        }
    }

    private static bool TryAppendJsonArrayString(
        string rawValue,
        ICollection<string> values,
        ISet<string> seenValues,
        bool splitCompositeValues)
    {
        if (!rawValue.StartsWith('[') || !rawValue.EndsWith(']'))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(rawValue);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in document.RootElement.EnumerateArray())
            {
                AppendClaimValues(item, values, seenValues, splitCompositeValues);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void AddUniqueValue(string? value, ICollection<string> values, ISet<string> seenValues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmedValue = value.Trim();
        if (!seenValues.Add(trimmedValue))
        {
            return;
        }

        values.Add(trimmedValue);
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
