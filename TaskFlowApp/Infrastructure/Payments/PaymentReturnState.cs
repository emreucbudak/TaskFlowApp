namespace TaskFlowApp.Infrastructure.Payments;

public sealed record PaymentReturnPayload(
    string? SessionId,
    Guid? CompanyId,
    string? PlanSlug,
    string? PlanName);

public static class PaymentReturnState
{
    private static readonly object SyncRoot = new();
    private static PaymentReturnPayload? _pendingPayload;

    public static void TryStoreFromCommandLine(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return;
        }

        foreach (var arg in args)
        {
            if (TryParseUriArgument(arg, out var uri))
            {
                TryStore(uri);
            }
        }
    }

    public static void TryStore(Uri uri)
    {
        if (!string.Equals(uri.Scheme, "taskflowapp", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = ParsePayload(uri);
        lock (SyncRoot)
        {
            _pendingPayload = payload;
        }
    }

    public static PaymentReturnPayload? TryGetPending()
    {
        lock (SyncRoot)
        {
            return _pendingPayload;
        }
    }

    public static void ClearPending()
    {
        lock (SyncRoot)
        {
            _pendingPayload = null;
        }
    }

    private static PaymentReturnPayload ParsePayload(Uri uri)
    {
        var query = ParseQuery(uri.Query);
        var sessionId = FirstNonEmpty(query, "session_id", "sessionid", "checkout_session_id", "checkoutsessionid");
        var planSlug = FirstNonEmpty(query, "plan_slug", "planslug");
        var planName = FirstNonEmpty(query, "plan_name", "planname");
        Guid? companyId = null;

        var companyIdValue = FirstNonEmpty(query, "company_id", "companyid");
        if (Guid.TryParse(companyIdValue, out var parsedCompanyId))
        {
            companyId = parsedCompanyId;
        }

        var clientReferenceId = FirstNonEmpty(query, "client_reference_id", "clientreferenceid");
        if (TryParseClientReferenceId(clientReferenceId, out parsedCompanyId, out var parsedPlanSlug))
        {
            companyId ??= parsedCompanyId;
            if (string.IsNullOrWhiteSpace(planSlug))
            {
                planSlug = parsedPlanSlug;
            }
        }

        return new PaymentReturnPayload(sessionId, companyId, planSlug, planName);
    }

    private static bool TryParseClientReferenceId(string? rawValue, out Guid companyId, out string? planSlug)
    {
        companyId = Guid.Empty;
        planSlug = null;

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var parts = rawValue
            .Trim()
            .Split("__", 2, StringSplitOptions.None);

        if (!Guid.TryParse(parts[0], out companyId))
        {
            return false;
        }

        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
        {
            planSlug = parts[1].Trim();
        }

        return true;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return result;
        }

        var queryText = query.StartsWith("?", StringComparison.Ordinal)
            ? query[1..]
            : query;

        foreach (var segment in queryText.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equalsIndex = segment.IndexOf('=');
            var key = equalsIndex >= 0 ? segment[..equalsIndex] : segment;
            var value = equalsIndex >= 0 ? segment[(equalsIndex + 1)..] : string.Empty;

            key = DecodeQueryPart(key);
            value = DecodeQueryPart(value);

            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            if (!result.ContainsKey(key))
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static string DecodeQueryPart(string value)
    {
        var safeValue = (value ?? string.Empty).Replace('+', ' ');
        return Uri.UnescapeDataString(safeValue).Trim();
    }

    private static string? FirstNonEmpty(Dictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static bool TryParseUriArgument(string? rawValue, out Uri uri)
    {
        uri = default!;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        var normalized = rawValue.Trim().Trim('"');
        return Uri.TryCreate(normalized, UriKind.Absolute, out uri);
    }
}
