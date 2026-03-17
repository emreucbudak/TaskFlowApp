namespace TaskFlowApp.Models.Identity;

public sealed record CompanyGroupDto
{
    public Guid GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public List<Guid> WorkerUserIds { get; init; } = [];
    public List<string> WorkerName { get; init; } = [];
    public List<string> DepartmenName { get; init; } = [];
    public List<Guid> LeaderUserIds { get; init; } = [];
}
