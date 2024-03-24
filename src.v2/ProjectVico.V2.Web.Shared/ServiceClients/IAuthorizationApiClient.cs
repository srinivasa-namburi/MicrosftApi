using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IAuthorizationApiClient : IServiceClient
{
    Task<UserInfoDTO?> StoreOrUpdateUserDetails(UserInfoDTO userInfoDto);
    Task<UserInfoDTO?> GetUserInfo(string providerSubjectId);
}