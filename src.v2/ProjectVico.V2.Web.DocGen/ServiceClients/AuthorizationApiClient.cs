using System.Net;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Web.Shared.ServiceClients;

namespace ProjectVico.V2.Web.DocGen.ServiceClients;

public class AuthorizationApiClient : BaseServiceClient<AuthorizationApiClient>, IAuthorizationApiClient
{
    public AuthorizationApiClient(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, ILogger<AuthorizationApiClient> logger) : base(httpClient, httpContextAccessor, logger)
    {
    }

    public async Task<UserInfoDTO?> StoreOrUpdateUserDetails(UserInfoDTO userInfoDto)
    {
        var response = await SendPostRequestMessage($"/api/authorization/store-or-update-user-details", userInfoDto, authorize:false);
        response?.EnsureSuccessStatusCode();

        return await response?.Content.ReadFromJsonAsync<UserInfoDTO>()!;
    }

    public async Task<UserInfoDTO?> GetUserInfo(string providerSubjectId)
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
}