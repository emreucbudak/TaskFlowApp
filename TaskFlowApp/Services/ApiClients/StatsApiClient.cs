using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.Stats;

namespace TaskFlowApp.Services.ApiClients;

public sealed class StatsApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Stats")
{
    public Task<PagedResultDto<WorkerStatsDto>> GetAllWorkersStatsByPeriodQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<WorkerStatsDto>>("GetAllWorkersStatsByPeriodQueryRequest", request, cancellationToken: cancellationToken);

    public Task<WorkerStatsDto> GetWorkerStatsByUserAndPeriodQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<WorkerStatsDto>("GetWorkerStatsByUserAndPeriodQueryRequest", request, cancellationToken: cancellationToken);

    public Task<WorkerStatsDto> GetWorkerStatsByUserAndPeriodAsync(Guid userId, DateOnly period, CancellationToken cancellationToken = default) =>
        PostForResultAsync<WorkerStatsDto>(
            "GetWorkerStatsByUserAndPeriodQueryRequest",
            new { UserId = userId, Period = period },
            cancellationToken: cancellationToken);
}
