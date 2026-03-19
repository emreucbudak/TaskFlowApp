using TaskFlowApp.Models.Identity;

namespace TaskFlowApp.Infrastructure.Helpers;

public static class GroupHelper
{
    public static bool IsGroupMember(CompanyGroupDto group, Guid userId, string? currentUserName)
    {
        if (group.WorkerUserIds.Contains(userId))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentUserName))
        {
            return false;
        }

        return group.WorkerName.Any(name =>
            string.Equals(name?.Trim(), currentUserName, StringComparison.OrdinalIgnoreCase));
    }

    public static List<CompanyGroupDto> NormalizeGroups(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .Where(group => !string.IsNullOrWhiteSpace(group.GroupName))
            .GroupBy(group => group.GroupName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(grouped => new CompanyGroupDto
            {
                GroupId = grouped
                    .Select(group => group.GroupId)
                    .FirstOrDefault(groupId => groupId != Guid.Empty),
                GroupName = grouped.First().GroupName.Trim(),
                WorkerUserIds = grouped
                    .SelectMany(group => group.WorkerUserIds)
                    .Where(workerId => workerId != Guid.Empty)
                    .Distinct()
                    .ToList(),
                WorkerName = grouped
                    .SelectMany(group => group.WorkerName)
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                DepartmenName = grouped
                    .SelectMany(group => group.DepartmenName)
                    .Select(name => name?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                LeaderUserIds = grouped
                    .SelectMany(group => group.LeaderUserIds)
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList()
            })
            .ToList();
    }
}
