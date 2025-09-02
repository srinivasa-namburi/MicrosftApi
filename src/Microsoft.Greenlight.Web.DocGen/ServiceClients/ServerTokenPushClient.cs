using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

internal sealed class ServerTokenPushClient : IServerTokenPushClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServerTokenPushClient> _logger;

    public ServerTokenPushClient(HttpClient httpClient, ILogger<ServerTokenPushClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task PushUserTokenAsync(UserTokenDTO dto, string bearerToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authorization/user-token");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        request.Content = new StringContent(JsonSerializer.Serialize(dto), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, ct);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed pushing user token. StatusCode={Status}", response.StatusCode);
            throw;
        }
    }
}
