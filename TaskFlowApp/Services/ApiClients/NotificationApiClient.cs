using TaskFlowApp.Infrastructure.Api;
using TaskFlowApp.Models.Common;
using TaskFlowApp.Models.Notification;

namespace TaskFlowApp.Services.ApiClients;

public sealed class NotificationApiClient(IApiClient apiClient) : ControllerApiClientBase(apiClient, "Notification")
{
    public Task CreateNotificationCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("CreateNotificationCommandRequest", request, cancellationToken: cancellationToken);

    public Task DeleteNotificationCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync("DeleteNotificationCommandRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<NotificationDto>> GetUserAllNotificationsQueriesRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<NotificationDto>>("GetUserAllNotificationsQueriesRequest", request, cancellationToken: cancellationToken);

    public Task<PagedResultDto<NotificationDto>> GetUserAllNotificationsAsync(Guid userId, int pageNumber = 1, int pageSize = 10, CancellationToken cancellationToken = default) =>
        PostForResultAsync<PagedResultDto<NotificationDto>>(
            "GetUserAllNotificationsQueriesRequest",
            new { userId, PageNumber = pageNumber, PageSize = pageSize },
            cancellationToken: cancellationToken);

    public Task<int> MarkUserNotificationsAsReadCommandRequestAsync(object request, CancellationToken cancellationToken = default) =>
        PostForResultAsync<int>("MarkUserNotificationsAsReadCommandRequest", request, cancellationToken: cancellationToken);

    public Task<int> MarkUserNotificationsAsReadAsync(Guid userId, int maxCount = 100, CancellationToken cancellationToken = default) =>
        PostForResultAsync<int>(
            "MarkUserNotificationsAsReadCommandRequest",
            new { userId, MaxCount = maxCount },
            cancellationToken: cancellationToken);
}
