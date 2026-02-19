using System.Text.Json;
using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.Identity;

namespace TaskFlowApp.Services.ApiClients;

public sealed class IdentityApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Identity")
{
    public Task AddDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("AddDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task AddGroupsCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("AddGroupsCommandRequest", request, cancellationToken: cancellationToken);

    public Task AddGroupsMemberCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("AddGroupsMemberCommandRequest", request, cancellationToken: cancellationToken);

    public Task AddUserToDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("AddUserToDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task CreateCompanyCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateCompanyCommandRequest", request, includeAuth: false, cancellationToken: cancellationToken);

    public Task DeleteCompanyCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteCompanyCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteGroupsCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteGroupsCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteGroupsMemberCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteGroupsMemberCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteUserFromDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteUserFromDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<JsonElement>> GetAllCompaniesQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<JsonElement>>("GetAllCompaniesQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<List<JsonElement>> GetAllCompanyGroupsQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<JsonElement>>("GetAllCompanyGroupsQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<JsonElement> GetDepartmentLeaderQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForJsonAsync("GetDepartmentLeaderQueryRequest", request, cancellationToken: cancellationToken);

    public Task<LoginCommandResponseDto> LoginCommandRequestAsync(LoginCommandRequestDto request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<LoginCommandResponseDto>("LoginCommandRequest", request, includeAuth: false, cancellationToken: cancellationToken);

    public Task RegisterCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("RegisterCommandRequest", request, includeAuth: false, cancellationToken: cancellationToken);

    public Task UpdateCompanyCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateCompanyCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task UpdateGroupsCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateGroupsCommandRequest", request, cancellationToken: cancellationToken);
}
