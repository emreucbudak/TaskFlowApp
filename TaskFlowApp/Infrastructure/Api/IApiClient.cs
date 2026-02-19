namespace TaskFlowApp.Infrastructure.Api;

public interface IApiClient
{
    Task PostAsync(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default);
    Task<T> PostAsync<T>(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default);
}
