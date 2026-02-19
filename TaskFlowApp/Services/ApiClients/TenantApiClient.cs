using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Tenant;

namespace TaskFlowApp.Services.ApiClients;

public sealed class TenantApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Tenant")
{
    public Task CreateCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);

    public Task<List<CompanyPlanDto>> GetAllCompanyPlanQueriesRequestAsync(object? request = null, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<CompanyPlanDto>>("GetAllCompanyPlanQueriesRequest", request ?? new { }, cancellationToken: cancellationToken);

    public Task UpdateCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);
}
