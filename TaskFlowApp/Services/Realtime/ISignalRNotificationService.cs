using TaskFlowApp.Models.Notification;

namespace TaskFlowApp.Services.Realtime;

public interface ISignalRNotificationService
{
    bool IsConnected { get; }

    event Action<NotificationDto>? NotificationReceived;
    event Action<string>? ConnectionStateChanged;

    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
