namespace TaskFlowApp.Models.Identity;

public sealed record CheckDepartmentLeadershipResponseDto
{
    public bool IsDepartmentLeader { get; init; }
    public Guid? DepartmentId { get; init; }
}
