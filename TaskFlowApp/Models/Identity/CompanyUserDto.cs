namespace TaskFlowApp.Models.Identity;

public sealed record CompanyUserDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public List<DepartmentMembershipDto> DepartmentMemberships { get; init; } = [];
}

public sealed record DepartmentMembershipDto
{
    public Guid DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
    public int DepartmentRoleId { get; init; }
}
