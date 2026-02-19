using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlowApp.Infrastructure.Session;

namespace TaskFlowApp.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient, IUserSession userSession) : IApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task PostAsync(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(route, payload, includeAuth);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        using var request = BuildRequest(route, payload, includeAuth);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        if (response.Content.Headers.ContentLength is 0)
        {
            return default!;
        }

        var result = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return result!;
    }

    private HttpRequestMessage BuildRequest(string route, object? payload, bool includeAuth)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, route)
        {
            Content = JsonContent.Create(payload ?? new { }, options: SerializerOptions)
        };

        if (includeAuth && !string.IsNullOrWhiteSpace(userSession.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userSession.AccessToken);
        }

        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new ApiException(
            $"API request failed with status {(int)response.StatusCode}.",
            (int)response.StatusCode,
            body);
    }
}
