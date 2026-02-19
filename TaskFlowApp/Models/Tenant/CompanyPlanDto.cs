using System.Text.Json;

namespace TaskFlowApp.Models.Tenant;

public sealed record CompanyPlanDto
{
    public string PlanName { get; init; } = string.Empty;
    public JsonElement PlanProperties { get; init; }
}
