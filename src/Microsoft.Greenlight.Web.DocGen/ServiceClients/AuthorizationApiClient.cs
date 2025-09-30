using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Web.Shared.ServiceClients;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

internal sealed class AuthorizationApiClient : BaseServiceClient<AuthorizationApiClient>, IAuthorizationApiClient
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthorizationApiClient> _logger;

    public AuthorizationApiClient(HttpClient httpClient, ILogger<AuthorizationApiClient> logger, AuthenticationStateProvider authStateProvider, IConfiguration configuration) : base(httpClient, logger, authStateProvider)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserInfoDTO?> StoreOrUpdateUserDetailsAsync(UserInfoDTO userInfoDto)
    {
        var response = await SendPostRequestMessage($"/api/authorization/store-or-update-user-details", userInfoDto, authorize: false);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<UserInfoDTO>()!;
    }

    public async Task FirstLoginSyncAsync(FirstLoginSyncRequest request, CancellationToken cancellationToken = default)
    {
        var response = await SendPostRequestMessage("/api/authorization/first-login-sync", request, authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    public async Task FirstLoginSyncWithBearerAsync(FirstLoginSyncRequest request, string bearerToken, CancellationToken cancellationToken = default)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/authorization/first-login-sync");
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        requestMessage.Content = JsonContent.Create(request);
        var response = await HttpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<UserInfoDTO?> GetUserInfoAsync(string providerSubjectId)
    {

        var response = await SendGetRequestMessage($"/api/authorization/{providerSubjectId}");

        // If we get a 404, it means that the user does not exist - return null
        if (response?.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response?.EnsureSuccessStatusCode();

        // Return user info if found - otherwise return null
        return await response?.Content.ReadFromJsonAsync<UserInfoDTO>()!;
    }

    public async Task<ThemePreference> GetThemePreferenceAsync(string providerSubjectId)
    {
        var response = await SendGetRequestMessage($"/api/authorization/theme/{providerSubjectId}");
        response?.EnsureSuccessStatusCode();
        return await response!.Content.ReadFromJsonAsync<ThemePreference>();
    }

    public async Task SetThemePreferenceAsync(ThemePreferenceDTO themePreferenceDto)
    {
        var response = await SendPostRequestMessage("/api/authorization/theme", themePreferenceDto);
        response?.EnsureSuccessStatusCode();
    }

    public Task<string> GetApiAddressAsync()
    {
        var apiAddress = AdminHelper.GetApiServiceUrl();
        return Task.FromResult(apiAddress ?? string.Empty);
    }

    public async Task SetUserTokenAsync(UserTokenDTO tokenDto)
    {
        var response = await SendPostRequestMessage("/api/authorization/user-token", tokenDto, authorize: true);
        response?.EnsureSuccessStatusCode();
    }

    public async Task SetUserTokenWithBearerAsync(UserTokenDTO tokenDto, string bearerToken, CancellationToken cancellationToken = default)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/api/authorization/user-token");
        requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);
        requestMessage.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(tokenDto), System.Text.Encoding.UTF8, "application/json");
        
        _logger.LogInformation("Sending POST request to /api/authorization/user-token with bearer token");
        var response = await HttpClient.SendAsync(requestMessage, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
