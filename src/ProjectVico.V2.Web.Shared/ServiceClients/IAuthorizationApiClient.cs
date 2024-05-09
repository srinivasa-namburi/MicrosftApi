using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IAuthorizationApiClient : IServiceClient
{
    Task<UserInfoDTO?> StoreOrUpdateUserDetailsAsync(UserInfoDTO userInfoDto);
    Task<UserInfoDTO?> GetUserInfoAsync(string providerSubjectId);

    Task<string> GetApiAddressAsync();
}