using System.Text.Json;
using TaskFlowApp.Infrastructure.Api;

namespace TaskFlowApp.Services.ApiClients;

public abstract class ControllerApiClientBase(IApiClient apiClient, string controllerName)
{
    protected Task<T> GetForResultAsync<T>(string action, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        return apiClient.GetAsync<T>($"api/{controllerName}/{action}", includeAuth, cancellationToken);
    }

    protected Task PostAsync(string action, object? request = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        return apiClient.PostAsync($"api/{controllerName}/{action}", request, includeAuth, cancellationToken);
    }

    protected Task<T> PostForResultAsync<T>(string action, object? request = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        return apiClient.PostAsync<T>($"api/{controllerName}/{action}", request, includeAuth, cancellationToken);
    }

    protected Task<JsonElement> PostForJsonAsync(string action, object? request = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        return apiClient.PostAsync<JsonElement>($"api/{controllerName}/{action}", request, includeAuth, cancellationToken);
    }
}
