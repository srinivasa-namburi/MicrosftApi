using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IAuthorizationApiClient : IServiceClient
{
    Task<UserInfoDTO?> StoreOrUpdateUserDetailsAsync(UserInfoDTO userInfoDto);
    Task FirstLoginSyncAsync(FirstLoginSyncRequest request, CancellationToken cancellationToken = default);
    Task FirstLoginSyncWithBearerAsync(FirstLoginSyncRequest request, string bearerToken, CancellationToken cancellationToken = default);
    Task<UserInfoDTO?> GetUserInfoAsync(string providerSubjectId);
    Task<ThemePreference> GetThemePreferenceAsync(string providerSubjectId);
    Task SetThemePreferenceAsync(ThemePreferenceDTO themePreferenceDto);
    Task<string> GetApiAddressAsync();
    Task SetUserTokenAsync(UserTokenDTO tokenDto);
    Task SetUserTokenWithBearerAsync(UserTokenDTO tokenDto, string bearerToken, CancellationToken cancellationToken = default);
}
