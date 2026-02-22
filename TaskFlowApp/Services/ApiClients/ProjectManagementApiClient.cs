using System.Text.Json;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.ProjectManagement;

namespace TaskFlowApp.Services.ApiClients;

public sealed class ProjectManagementApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "ProjectManagement")
{
    public Task CreateIndividualTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateIndividualTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task CreateSubTaskAnswerCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateSubTaskAnswerCommandRequest", request, cancellationToken: cancellationToken);

    public Task CreateSubTasksCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateSubTasksCommandRequest", request, cancellationToken: cancellationToken);

    public Task CreateTasksCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateTasksCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteIndividualTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteIndividualTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task<JsonElement> DeleteSubTaskAnswerCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForJsonAsync("DeleteSubTaskAnswerCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteSubTasksCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteSubTasksCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteTasksCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteTasksCommandRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<JsonElement>> GetAllSubTaskAnswerQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<JsonElement>>("GetAllSubTaskAnswerQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<JsonElement>> GetAllSubTasksQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<JsonElement>>("GetAllSubTasksQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<JsonElement>> GetAllTasksQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<JsonElement>>("GetAllTasksQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<CompanyTaskDto>> GetAllTasksByCompanyIdAsync(Guid companyId, int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<CompanyTaskDto>>(
            "GetAllTasksQueriesRequest",
            new { CompanyId = companyId, pageNumber, pageSize },
            cancellationToken: cancellationToken);

    public Task<JsonElement> GetIndividualTaskByIdQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForJsonAsync("GetIndividualTaskByIdQueryRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<JsonElement>> GetIndividualTasksByUserIdQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<JsonElement>>("GetIndividualTasksByUserIdQueryRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<IndividualTaskDto>> GetIndividualTasksByUserIdAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<IndividualTaskDto>>(
            "GetIndividualTasksByUserIdQueryRequest",
            new { UserId = userId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task UpdateIndividualTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateIndividualTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateSubTaskAnswerCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateSubTaskAnswerCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateSubTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateSubTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateSubTasksStatusCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateSubTasksStatusCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateTaskCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateTaskCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateTaskStatusCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateTaskStatusCommandRequest", request, cancellationToken: cancellationToken);
}
