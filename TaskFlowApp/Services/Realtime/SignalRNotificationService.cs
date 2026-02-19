using Microsoft.AspNetCore.SignalR.Client;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Notification;

namespace TaskFlowApp.Services.Realtime;

public sealed class SignalRNotificationService(IUserSession userSession) : ISignalRNotificationService
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<NotificationDto>? NotificationReceived;
    public event Action<string>? ConnectionStateChanged;

    public async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userSession.AccessToken))
        {
            throw new InvalidOperationException("SignalR baglantisi icin token bulunamadi.");
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is null)
            {
                _hubConnection = CreateConnection();
            }

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync(cancellationToken);
                ConnectionStateChanged?.Invoke("Connected");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_hubConnection is null)
            {
                return;
            }

            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                await _hubConnection.StopAsync(cancellationToken);
            }

            await _hubConnection.DisposeAsync();
            _hubConnection = null;
            ConnectionStateChanged?.Invoke("Disconnected");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private HubConnection CreateConnection()
    {
        var hubUrl = $"{AppEndpoints.ApiBaseUrl.TrimEnd('/')}/{AppEndpoints.NotificationHubPath}";
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(userSession.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<string, string>("NewMessage", (title, message) =>
        {
            var dto = new NotificationDto
            {
                Title = title,
                Description = message,
                SendTime = DateTime.UtcNow,
                IsRead = false
            };

            NotificationReceived?.Invoke(dto);
        });

        connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke("Reconnecting");
            return Task.CompletedTask;
        };

        connection.Reconnected += _ =>
        {
            ConnectionStateChanged?.Invoke("Connected");
            return Task.CompletedTask;
        };

        connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke("Disconnected");
            return Task.CompletedTask;
        };

        return connection;
    }
}
