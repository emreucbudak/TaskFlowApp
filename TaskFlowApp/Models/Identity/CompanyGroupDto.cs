namespace TaskFlowApp.Models.Identity;

public sealed record CompanyGroupDto
{
    public string GroupName { get; init; } = string.Empty;
    public List<string> WorkerName { get; init; } = [];
    public List<string> DepartmenName { get; init; } = [];
}
