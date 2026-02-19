namespace TaskFlowApp.Services.Realtime;

public interface IRealtimeConnectionManager
{
    Task ConnectAllAsync(CancellationToken cancellationToken = default);
    Task DisconnectAllAsync(CancellationToken cancellationToken = default);
}
