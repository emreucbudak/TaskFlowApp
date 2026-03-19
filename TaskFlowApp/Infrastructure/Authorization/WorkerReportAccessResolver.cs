using TaskFlowApp.Infrastructure.Constants;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Services.ApiClients;

namespace TaskFlowApp.Infrastructure.Authorization;

public sealed class WorkerReportAccessResolver(
    IUserSession userSession,
    IdentityApiClient identityApiClient) : IWorkerReportAccessResolver
{
    private readonly SemaphoreSlim stateLock = new(1, 1);
    private WorkerReportAccessState? cachedState;
    private Guid? cachedUserId;
    private Guid? cachedCompanyId;

    public async Task<WorkerReportAccessState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        if (!string.Equals(userSession.Role, AppRoles.Worker, StringComparison.OrdinalIgnoreCase) ||
            userSession.UserId is null ||
            userSession.CompanyId is null)
        {
            return WorkerReportAccessState.None;
        }

        var currentUserId = userSession.UserId.Value;
        var currentCompanyId = userSession.CompanyId.Value;

        if (cachedState is not null &&
            cachedUserId == currentUserId &&
            cachedCompanyId == currentCompanyId)
        {
            return cachedState;
        }

        await stateLock.WaitAsync(cancellationToken);
        try
        {
            if (cachedState is not null &&
                cachedUserId == currentUserId &&
                cachedCompanyId == currentCompanyId)
            {
                return cachedState;
            }

            var departments = await identityApiClient.GetAllCompanyDepartmentsAsync(currentCompanyId, cancellationToken) ?? [];

            foreach (var department in departments.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (department.Id == Guid.Empty)
                {
                    continue;
                }

                try
                {
                    var leaderId = await identityApiClient.GetDepartmentLeaderIdAsync(department.Id, cancellationToken);
                    if (leaderId == currentUserId)
                    {
                        cachedUserId = currentUserId;
                        cachedCompanyId = currentCompanyId;
                        cachedState = new WorkerReportAccessState(true, department.Id, department.Name);
                        return cachedState;
                    }
                }
                catch
                {
                    // Lider bilgisi tek bir departman icin alinamazsa diger departmanlari kontrol etmeye devam et.
                }
            }

            cachedUserId = currentUserId;
            cachedCompanyId = currentCompanyId;
            cachedState = WorkerReportAccessState.None;
            return cachedState;
        }
        finally
        {
            stateLock.Release();
        }
    }
}