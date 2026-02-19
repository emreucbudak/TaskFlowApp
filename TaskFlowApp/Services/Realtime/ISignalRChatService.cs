using TaskFlowApp.Models.Chat;

namespace TaskFlowApp.Services.Realtime;

public interface ISignalRChatService
{
    bool IsConnected { get; }

    event Action<MessageDto>? PrivateMessageReceived;
    event Action<string>? ConnectionStateChanged;

    Task EnsureConnectedAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task SendPrivateMessageAsync(string targetUserId, string message, CancellationToken cancellationToken = default);
    Task JoinGroupAsync(string groupName, CancellationToken cancellationToken = default);
    Task LeaveGroupAsync(string groupName, CancellationToken cancellationToken = default);
}
