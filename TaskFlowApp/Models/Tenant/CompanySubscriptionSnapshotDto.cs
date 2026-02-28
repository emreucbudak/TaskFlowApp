namespace TaskFlowApp.Models.Tenant;

public sealed record CompanySubscriptionSnapshotDto
{
    public Guid CompanyId { get; init; }
    public bool HasActiveSubscription { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public int PlanPrice { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? StartDateUtc { get; init; }
    public DateTime? NextBillingDateUtc { get; init; }
    public int CurrentUserCount { get; init; }
    public int CurrentGroupCount { get; init; }
    public int CurrentIndividualTaskCount { get; init; }
    public int UserLimit { get; init; }
    public int TeamLimit { get; init; }
    public int IndividualTaskLimit { get; init; }
    public bool IsInternalReportingEnabled { get; init; }
}

public sealed record ActivateCompanySubscriptionResponseDto
{
    public Guid CompanyId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartDateUtc { get; init; }
    public DateTime NextBillingDateUtc { get; init; }
}

public sealed record CreateStripeCheckoutSessionRequestDto
{
    public Guid CompanyId { get; init; }
    public string? PlanSlug { get; init; }
    public string? PlanName { get; init; }
}

public sealed record CreateStripeCheckoutSessionResponseDto
{
    public string? SessionId { get; init; }
    public string CheckoutUrl { get; init; } = string.Empty;
    public string PlanName { get; init; } = string.Empty;
    public int PlanPrice { get; init; }
}

public sealed record ConfirmStripePaymentAndActivateRequestDto
{
    public Guid CompanyId { get; init; }
    public string? PlanSlug { get; init; }
    public string? PlanName { get; init; }
    public string SessionId { get; init; } = string.Empty;
}

public sealed record ConfirmStripePaymentAndActivateResponseDto
{
    public Guid CompanyId { get; init; }
    public string PlanName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartDateUtc { get; init; }
    public DateTime NextBillingDateUtc { get; init; }
    public bool Processed { get; init; }
    public bool AlreadyProcessed { get; init; }
    public string Message { get; init; } = string.Empty;
}
