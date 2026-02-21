using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlowApp.Infrastructure;
using TaskFlowApp.Infrastructure.Session;

namespace TaskFlowApp.Infrastructure.Api;

public sealed class ApiClient(HttpClient httpClient, IUserSession userSession) : IApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly Uri[] BaseAddressCandidates = AppEndpoints.ApiBaseUrls.Select(static url => new Uri(url)).ToArray();
    private Uri? resolvedBaseAddress;

    public async Task PostAsync(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithFallbackAsync(route, payload, includeAuth, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<T> PostAsync<T>(string route, object? payload = null, bool includeAuth = true, CancellationToken cancellationToken = default)
    {
        using var response = await SendWithFallbackAsync(route, payload, includeAuth, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        if (response.Content.Headers.ContentLength is 0)
        {
            return default!;
        }

        var result = await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        return result!;
    }

    private async Task<HttpResponseMessage> SendWithFallbackAsync(string route, object? payload, bool includeAuth, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        foreach (var baseAddress in GetAddressCandidates())
        {
            var requestUri = new Uri(baseAddress, route);

            try
            {
                var response = await SendWithManualRedirectAsync(requestUri, payload, includeAuth, cancellationToken);
                resolvedBaseAddress = baseAddress;
                return response;
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
            }
        }

        if (lastException is not null)
        {
            throw lastException;
        }

        throw new HttpRequestException("No reachable API endpoint was found.");
    }

    private async Task<HttpResponseMessage> SendWithManualRedirectAsync(
        Uri requestUri,
        object? payload,
        bool includeAuth,
        CancellationToken cancellationToken)
    {
        const int maxRedirectCount = 3;
        var currentUri = requestUri;

        for (var redirectCount = 0; redirectCount <= maxRedirectCount; redirectCount++)
        {
            using var request = BuildRequest(currentUri, payload, includeAuth);
            var response = await httpClient.SendAsync(request, cancellationToken);

            if (!IsRedirectStatusCode(response.StatusCode) || response.Headers.Location is null)
            {
                return response;
            }

            if (redirectCount == maxRedirectCount)
            {
                return response;
            }

            var redirectUri = response.Headers.Location.IsAbsoluteUri
                ? response.Headers.Location
                : new Uri(currentUri, response.Headers.Location);

            response.Dispose();
            currentUri = redirectUri;
        }

        throw new HttpRequestException("Unexpected redirect loop while calling API.");
    }

    private static bool IsRedirectStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.Moved
            or HttpStatusCode.MovedPermanently
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.RedirectKeepVerb
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;
    }

    private IEnumerable<Uri> GetAddressCandidates()
    {
        if (resolvedBaseAddress is not null)
        {
            yield return resolvedBaseAddress;
        }

        foreach (var candidate in BaseAddressCandidates)
        {
            if (candidate == resolvedBaseAddress)
            {
                continue;
            }

            yield return candidate;
        }
    }

    private HttpRequestMessage BuildRequest(Uri requestUri, object? payload, bool includeAuth)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
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
