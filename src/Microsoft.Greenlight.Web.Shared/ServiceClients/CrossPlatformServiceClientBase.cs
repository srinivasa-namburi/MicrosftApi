using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Web.Shared.Auth;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// Base HTTP client that works in both Blazor WebAssembly and server-side processes.
/// - Optional authentication: can attach Bearer token when requested.
/// - Token sources: AuthenticationStateProvider claims or fallback to /configuration/token endpoint if available.
/// - Supports JSON payloads and file upload from both IBrowserFile and IFormFile.
/// </summary>
public abstract class CrossPlatformServiceClientBase<T> where T : IServiceClient
{
    protected readonly ILogger<T> Logger;
    protected readonly HttpClient HttpClient;

    private readonly AuthenticationStateProvider? _authStateProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    protected CrossPlatformServiceClientBase(HttpClient httpClient, ILogger<T> logger)
    {
        Logger = logger;
        HttpClient = httpClient;
    }

    protected CrossPlatformServiceClientBase(HttpClient httpClient, ILogger<T> logger, AuthenticationStateProvider authStateProvider)
        : this(httpClient, logger)
    {
        _authStateProvider = authStateProvider;
    }

    protected async Task<HttpResponseMessage?> SendGetRequestMessage(string requestUri, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                requestMessage.Headers.Authorization = new("Bearer", token);
            }
        }

        Logger.LogInformation("Sending GET request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    protected async Task<HttpResponseMessage> SendDeleteRequestMessage(string requestUri, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Delete, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                requestMessage.Headers.Authorization = new("Bearer", token);
            }
        }

        Logger.LogInformation("Sending DELETE request to {RequestUri}", requestUri);
        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    protected async Task<HttpResponseMessage?> SendPostRequestMessage(string requestUri, object? payload, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                requestMessage.Headers.Authorization = new("Bearer", token);
            }
        }

        if (payload != null)
        {
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        }
        else
        {
            Logger.LogWarning("Sending POST request to {RequestUri} with empty payload", requestUri);
        }

        Logger.LogInformation("Sending POST request to {RequestUri}", requestUri);

        return await HttpClient.SendAsync(requestMessage);
    }

    protected async Task<HttpResponseMessage?> SendPutRequestMessage(string requestUri, object? pocoPayload, bool authorize = false)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Put, requestUri);
        if (authorize)
        {
            var token = await GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                requestMessage.Headers.Authorization = new("Bearer", token);
            }
        }

        if (pocoPayload == null)
        {
            Logger.LogWarning("Sending PUT request to {RequestUri} with empty payload", requestUri);
        }

        requestMessage.Content = new StringContent(JsonSerializer.Serialize(pocoPayload, JsonOptions), Encoding.UTF8, "application/json");

        Logger.LogInformation("Sending PUT request to {RequestUri}", requestUri);

        var response = await HttpClient.SendAsync(requestMessage);
        return response;
    }

    /// <summary>
    /// Try to obtain an access token suitable for Authorization header.
    /// Prefers AuthenticationStateProvider claims; falls back to the host's /configuration/token endpoint if present.
    /// Returns empty string if no token is available.
    /// </summary>
    public virtual async Task<string> GetAccessTokenAsync()
    {
        try
        {
            // 1) Try Blazor/WASM or server-side auth state provider
            if (_authStateProvider != null)
            {
                var authInfo = await _authStateProvider.GetAuthenticationStateAsync();
                if (authInfo.User.Identity?.IsAuthenticated == true)
                {
                    var userInfo = UserInfo.FromClaimsPrincipal(authInfo.User);
                    if (!string.IsNullOrWhiteSpace(userInfo.Token))
                    {
                        return userInfo.Token!;
                    }
                }
            }

            // 2) Fallback: try the local token endpoint used by Web.DocGen hosts
            try
            {
                using var resp = await HttpClient.GetAsync("/configuration/token");
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    var token = await resp.Content.ReadAsStringAsync();
                    return token ?? string.Empty;
                }
            }
            catch
            {
                // ignore, no token endpoint available
            }
        }
        catch
        {
            // Ignore token errors; caller may not require authorization
        }

        return string.Empty;
    }
}
