namespace TaskFlowApp.Models.Tenant;

public sealed record CompanyPlanDto
{
    public string PlanName { get; init; } = string.Empty;
    public int PlanPrice { get; init; }
    public PlanPropertiesDto PlanProperties { get; init; } = new();
}

public sealed record PlanPropertiesDto
{
    public int PeopleAddedLimit { get; init; }
    public int TeamLimit { get; init; }
    public int IndividualTaskLimit { get; init; }
    public bool IsInternalReportingEnabled { get; init; }
}
