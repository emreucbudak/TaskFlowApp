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

    public static List<CompanyGroupDto> ResolveUserGroups(
        IEnumerable<CompanyGroupDto> groups,
        Guid userId,
        IReadOnlyDictionary<Guid, string> userNameMap)
    {
        userNameMap.TryGetValue(userId, out var currentUserName);

        return groups
            .Where(group => IsGroupMember(group, userId, currentUserName))
            .ToList();
    }

    public static List<Guid> ResolveGroupMemberIds(
        IEnumerable<CompanyGroupDto> groups,
        IReadOnlyDictionary<string, Guid> userIdByNameMap)
    {
        var memberIds = new HashSet<Guid>(
            groups
                .SelectMany(group => group.WorkerUserIds)
                .Where(userId => userId != Guid.Empty));

        foreach (var workerName in groups.SelectMany(group => group.WorkerName))
        {
            var normalizedWorkerName = workerName?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedWorkerName))
            {
                continue;
            }

            if (userIdByNameMap.TryGetValue(normalizedWorkerName, out var workerId) && workerId != Guid.Empty)
            {
                memberIds.Add(workerId);
            }
        }

        return memberIds.ToList();
    }

    public static List<Guid> ResolveGroupMemberIds(IEnumerable<CompanyGroupDto> groups)
    {
        return groups
            .SelectMany(group => group.WorkerUserIds)
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();
    }
}
