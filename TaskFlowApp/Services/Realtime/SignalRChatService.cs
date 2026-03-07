using Microsoft.AspNetCore.SignalR.Client;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Session;
using TaskFlowApp.Models.Chat;

namespace TaskFlowApp.Services.Realtime;

public sealed class SignalRChatService(IUserSession userSession) : ISignalRChatService
{
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _hubConnection;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

    public event Action<MessageDto>? PrivateMessageReceived;
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

    public async Task SendPrivateMessageAsync(string targetUserId, string message, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await _hubConnection!.InvokeAsync("SendPrivateMessage", cancellationToken, targetUserId, message);
    }

    public async Task JoinGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await _hubConnection!.InvokeAsync("JoinGroup", cancellationToken, groupName);
    }

    public async Task LeaveGroupAsync(string groupName, CancellationToken cancellationToken = default)
    {
        await EnsureConnectedAsync(cancellationToken);
        await _hubConnection!.InvokeAsync("LeaveGroup", cancellationToken, groupName);
    }

    private HubConnection CreateConnection()
    {
        var hubUrl = $"{AppEndpoints.ApiBaseUrl.TrimEnd('/')}/{AppEndpoints.ChatHubPath}";
        var connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(userSession.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        connection.On<string, string, DateTime>("ReceivePrivateMessage", (senderId, message, sendTime) =>
        {
            var senderGuid = Guid.TryParse(senderId, out var parsedSenderId) ? parsedSenderId : Guid.Empty;
            PrivateMessageReceived?.Invoke(new MessageDto
            {
                Id = Guid.NewGuid(),
                SenderId = senderGuid,
                Content = message,
                SendTime = NormalizeSendTime(sendTime),
                IsRead = false,
                IsDelivered = true
            });
        });

        connection.On<string, MessageDto>("ReceivePrivateMessage", (_, message) =>
        {
            if (message is null)
            {
                return;
            }

            PrivateMessageReceived?.Invoke(message with
            {
                Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id,
                IsDelivered = true,
                SendTime = NormalizeSendTime(message.SendTime)
            });
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

    private static DateTime NormalizeSendTime(DateTime sendTime)
    {
        return sendTime.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(sendTime, DateTimeKind.Utc)
            : sendTime;
    }
}