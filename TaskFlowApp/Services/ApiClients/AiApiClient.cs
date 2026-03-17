using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Ai;

namespace TaskFlowApp.Services.ApiClients;

public sealed class AiApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Ai")
{
    public Task<DailySummaryDto> GetDailySummaryAsync(
        Guid userId,
        Guid companyId,
        bool isDepartmentLeader,
        Guid? departmentId,
        CancellationToken cancellationToken = default) =>
        PostForResultAsync<DailySummaryDto>("GetDailySummaryRequest", new
        {
            UserId = userId,
            CompanyId = companyId,
            IsDepartmentLeader = isDepartmentLeader,
            DepartmentId = departmentId
        }, cancellationToken: cancellationToken);
}
