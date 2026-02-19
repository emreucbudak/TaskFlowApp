using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Chat;

namespace TaskFlowApp.Services.ApiClients;

public sealed class ChatApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Chat")
{
    public Task CreateMessageCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateMessageCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteMessageCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteMessageCommandRequest", request, cancellationToken: cancellationToken);

    public Task<List<MessageDto>> GetMessagesBetweenUsersQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<MessageDto>>("GetMessagesBetweenUsersQueryRequest", request, cancellationToken: cancellationToken);

    public Task<List<MessageDto>> GetMessagesByGroupIdQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<MessageDto>>("GetMessagesByGroupIdQueryRequest", request, cancellationToken: cancellationToken);

    public Task<List<MessageDto>> GetMessagesByUserIdQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<MessageDto>>("GetMessagesByUserIdQueryRequest", request, cancellationToken: cancellationToken);

    public Task<List<MessageDto>> GetMessagesByUserIdAsync(Guid userId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<MessageDto>>(
            "GetMessagesByUserIdQueryRequest",
            new { UserId = userId, Page = page, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<int> GetUnreadMessageCountQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<int>("GetUnreadMessageCountQueryRequest", request, cancellationToken: cancellationToken);

    public Task<int> GetUnreadMessageCountAsync(Guid userId, CancellationToken cancellationToken = default) =>
        PostForResultAsync<int>("GetUnreadMessageCountQueryRequest", new { UserId = userId }, cancellationToken: cancellationToken);

    public Task MarkAsDeliveredCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("MarkAsDeliveredCommandRequest", request, cancellationToken: cancellationToken);

    public Task<List<MessageDto>> SearchMessagesQueryRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<List<MessageDto>>("SearchMessagesQueryRequest", request, cancellationToken: cancellationToken);

    public Task UpdateMessageCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("UpdateMessageCommandRequest", request, cancellationToken: cancellationToken);
}
