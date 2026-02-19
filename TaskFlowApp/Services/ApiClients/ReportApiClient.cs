using System.Text.Json;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.Report;

namespace TaskFlowApp.Services.ApiClients;

public sealed class ReportApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Report")
{
    public Task CreateReportCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateReportCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteReportCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteReportCommandRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<ReportDto>> GetAllReportsQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<ReportDto>>("GetAllReportsQueryRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<ReportDto>> GetAllReportsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<ReportDto>>(
            "GetAllReportsQueryRequest",
            new { Page = page, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<JsonElement> GetReportByIdQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForJsonAsync("GetReportByIdQueryRequest", request, cancellationToken: cancellationToken);

    public Task UpdateReportCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateReportCommandRequest", request, cancellationToken: cancellationToken);
}
