using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Net;
using System.Net.Http.Json;

namespace Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;

public class AuthorizationApiClient : WebAssemblyBaseServiceClient<AuthorizationApiClient>,  IAuthorizationApiClient
{
    public AuthorizationApiClient(HttpClient httpClient, ILogger<AuthorizationApiClient> logger, AuthenticationStateProvider authStateProvider) : base(httpClient, logger, authStateProvider)
    {
    }

    public async Task<UserInfoDTO?> StoreOrUpdateUserDetailsAsync(UserInfoDTO userInfoDto)
    {
        var response = await SendPostRequestMessage($"/api/authorization/store-or-update-user-details", userInfoDto, authorize:false);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<UserInfoDTO>()!;
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
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ThemePreference>();
    }

    public async Task SetThemePreferenceAsync(ThemePreferenceDTO themePreferenceDto)
    {
        var response = await SendPostRequestMessage("/api/authorization/theme", themePreferenceDto);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> GetApiAddressAsync()
    {
        var response = await SendGetRequestMessage("/api-address");
        response?.EnsureSuccessStatusCode();
        var responseString = await response?.Content.ReadAsStringAsync()!;
        responseString = responseString.Replace("\"","").TrimEnd('/');
        return responseString;
    }
}
