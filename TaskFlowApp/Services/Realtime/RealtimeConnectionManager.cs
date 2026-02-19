namespace TaskFlowApp.Services.Realtime;

public sealed class RealtimeConnectionManager(
    ISignalRChatService signalRChatService,
    ISignalRNotificationService signalRNotificationService) : IRealtimeConnectionManager
{
    public async Task ConnectAllAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            signalRChatService.EnsureConnectedAsync(cancellationToken),
            signalRNotificationService.EnsureConnectedAsync(cancellationToken));
    }

    public async Task DisconnectAllAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(
            signalRChatService.DisconnectAsync(cancellationToken),
            signalRNotificationService.DisconnectAsync(cancellationToken));
    }
}
