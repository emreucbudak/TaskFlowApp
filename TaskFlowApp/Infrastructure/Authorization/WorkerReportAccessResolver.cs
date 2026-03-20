using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.Infrastructure.Authorization;

public sealed class WorkerReportAccessResolver(
    IUserSession userSession,
    IdentityApiClient identityApiClient) : IWorkerReportAccessResolver
{
    public async Task<WorkerReportAccessState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(userSession.Role, AppRoles.Worker, StringComparison.OrdinalIgnoreCase))
        {
            return WorkerReportAccessState.None;
        }

        if (userSession.UserId is not { } userId)
        {
            return WorkerReportAccessState.None;
        }

        try
        {
            var result = await identityApiClient.CheckDepartmentLeadershipAsync(userId, cancellationToken);

            if (!result.IsDepartmentLeader)
            {
                return WorkerReportAccessState.None;
            }

            var departmentName = userSession.DepartmentNames.Count > 0
                ? userSession.DepartmentNames[0]
                : string.Empty;

            return new WorkerReportAccessState(true, result.DepartmentId, departmentName);
        }
        catch
        {
            return WorkerReportAccessState.None;
        }
    }
}
