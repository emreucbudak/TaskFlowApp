using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.ProjectManagement;

namespace TaskFlowApp.Services.ApiClients;

public sealed class ProjectManagementApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "ProjectManagement")
{
    public Task CreateIndividualTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateIndividualTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task CreateGroupTaskWithSubTasksCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateGroupTaskWithSubTasksCommandRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<CompanyTaskDto>> GetAllTasksByCompanyIdAsync(Guid companyId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<CompanyTaskDto>>(
            "GetAllTasksQueriesRequest",
            new { CompanyId = companyId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<PagedResultDto<CompanyTaskDto>> GetGroupTasksByAssignedUsersAsync(IReadOnlyCollection<Guid> assignedUserIds, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<CompanyTaskDto>>(
            "GetGroupTasksByAssignedUsersQueryRequest",
            new { AssignedUserIds = assignedUserIds, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<PagedResultDto<IndividualTaskDto>> GetIndividualTasksByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<IndividualTaskDto>>(
            "GetIndividualTasksByUserIdQueryRequest",
            new { UserId = userId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken: cancellationToken);
}
