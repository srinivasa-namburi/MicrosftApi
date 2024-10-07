using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IAuthorizationApiClient : IServiceClient
{
    Task<UserInfoDTO?> StoreOrUpdateUserDetailsAsync(UserInfoDTO userInfoDto);
    Task<UserInfoDTO?> GetUserInfoAsync(string providerSubjectId);

    Task<string> GetApiAddressAsync();
}
