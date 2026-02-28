using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Tenant;

namespace TaskFlowApp.Services.ApiClients;

public sealed class TenantApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Tenant")
{
    public Task<List<CompanyPlanDto>> GetCompanyPlansAsync(CancellationToken cancellationToken = default) =>
        GetForResultAsync<List<CompanyPlanDto>>("CompanyPlans", includeAuth: false, cancellationToken: cancellationToken);

    public Task<CompanySubscriptionSnapshotDto> GetCompanySubscriptionSnapshotAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<CompanySubscriptionSnapshotDto>(
            "GetCompanySubscriptionSnapshotRequest",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken);

    public Task<ActivateCompanySubscriptionResponseDto> ActivateCompanySubscriptionRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<ActivateCompanySubscriptionResponseDto>("ActivateCompanySubscriptionRequest", request, cancellationToken: cancellationToken);

    public Task<CreateStripeCheckoutSessionResponseDto> CreateStripeCheckoutSessionAsync(
        CreateStripeCheckoutSessionRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostForResultAsync<CreateStripeCheckoutSessionResponseDto>(
            "CreateStripeCheckoutSessionRequest",
            request,
            includeAuth: false,
            cancellationToken: cancellationToken);

    public Task<ConfirmStripePaymentAndActivateResponseDto> ConfirmStripePaymentAndActivateRequestAsync(
        ConfirmStripePaymentAndActivateRequestDto request,
        CancellationToken cancellationToken = default) =>
        PostForResultAsync<ConfirmStripePaymentAndActivateResponseDto>(
            "ConfirmStripePaymentAndActivateRequest",
            request,
            includeAuth: false,
            cancellationToken: cancellationToken);

    public Task CreateCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);

    public Task<List<CompanyPlanDto>> GetAllCompanyPlanQueriesRequestAsync(object? request = null, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<CompanyPlanDto>>("GetAllCompanyPlanQueriesRequest", request ?? new { }, cancellationToken: cancellationToken);

    public Task UpdateCompanyPlanCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateCompanyPlanCommandRequest", request, cancellationToken: cancellationToken);
}
