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

    public Task AddUserToDepartmentCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("AddUserToDepartmentCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteGroupsCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteGroupsCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteWorkerCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteWorkerCommandRequest", request, cancellationToken: cancellationToken);

    public Task ChangeUserPasswordCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("ChangeUserPasswordCommandRequest", request, cancellationToken: cancellationToken);

    public Task<List<CompanyGroupDto>> GetAllCompanyGroupsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<CompanyGroupDto>>(
            "GetAllCompanyGroupsQueriesRequest",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken);

    public Task<List<DepartmentDto>> GetAllCompanyDepartmentsAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<DepartmentDto>>(
            "GetAllCompanyDepartmentsQueriesRequest",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken);

    public Task<List<CompanyUserDto>> GetAllCompanyUsersAsync(Guid companyId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<CompanyUserDto>>(
            "GetAllCompanyUsersQueriesRequest",
            new { CompanyId = companyId },
            cancellationToken: cancellationToken);

    public Task<PagedResultDto<CompanyUserDto>> SearchCompanyUsersAsync(Guid companyId, string searchText, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<CompanyUserDto>>(
            "SearchCompanyUsersQueryRequest",
            new { CompanyId = companyId, SearchText = searchText, Page = page, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<LoginCommandResponseDto> LoginCommandRequestAsync(LoginCommandRequestDto request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<LoginCommandResponseDto>("LoginCommandRequest", request, includeAuth: false, cancellationToken: cancellationToken);

    public Task<CurrentUserContextDto> GetCurrentUserContextAsync(CancellationToken cancellationToken = default) =>
        GetForResultAsync<CurrentUserContextDto>("GetCurrentUserContext", includeAuth: true, cancellationToken: cancellationToken);

    public Task RegisterCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("RegisterCommandRequest", request, includeAuth: false, cancellationToken: cancellationToken);

    public async Task<Guid> GetDepartmentLeaderIdAsync(Guid departmentId, CancellationToken cancellationToken = default)
    {
        var result = await PostForResultAsync<DepartmentLeaderResponseDto>(
            "GetDepartmentLeaderQueryRequest",
            new { DepartmentId = departmentId },
            cancellationToken: cancellationToken);
        return result.LeaderId;
    }

    public Task SubmitGroupActivityAsync(Guid groupId, string title, string description, CancellationToken cancellationToken = default) =>
        PostAsync("SubmitGroupActivityCommandRequest", new { GroupId = groupId, Title = title, Description = description }, cancellationToken: cancellationToken);

    public Task ApproveGroupActivityAsync(Guid activityId, string? note = null, CancellationToken cancellationToken = default) =>
        PostAsync("ApproveGroupActivityCommandRequest", new { ActivityId = activityId, Note = note }, cancellationToken: cancellationToken);

    public Task RejectGroupActivityAsync(Guid activityId, string? note = null, CancellationToken cancellationToken = default) =>
        PostAsync("RejectGroupActivityCommandRequest", new { ActivityId = activityId, Note = note }, cancellationToken: cancellationToken);

    public Task<List<GroupActivityDto>> GetGroupActivitiesAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<GroupActivityDto>>(
            "GetGroupActivitiesQueryRequest",
            new { GroupId = groupId },
            cancellationToken: cancellationToken);

    public Task<Guid> CreateGroupEventAsync(CreateGroupEventRequestDto request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<Guid>(
            "CreateGroupEventCommandRequest",
            request,
            cancellationToken: cancellationToken);

    public Task DeleteGroupEventAsync(Guid groupEventId, CancellationToken cancellationToken = default) =>
        PostAsync(
            "DeleteGroupEventCommandRequest",
            new { GroupEventId = groupEventId },
            cancellationToken: cancellationToken);

    public Task<List<GroupEventDto>> GetGroupEventsAsync(Guid groupId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<GroupEventDto>>(
            "GetGroupEventsQueryRequest",
            new { GroupId = groupId },
            cancellationToken: cancellationToken);

    public Task<CheckDepartmentLeadershipResponseDto> CheckDepartmentLeadershipAsync(Guid userId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<CheckDepartmentLeadershipResponseDto>(
            "CheckDepartmentLeadershipQueryRequest",
            new { UserId = userId },
            cancellationToken: cancellationToken);
}
