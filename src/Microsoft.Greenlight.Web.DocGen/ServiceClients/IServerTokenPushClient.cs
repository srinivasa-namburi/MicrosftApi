using Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

namespace Microsoft.Greenlight.Web.DocGen.ServiceClients;

public interface IServerTokenPushClient
{
    Task PushUserTokenAsync(UserTokenDTO dto, string bearerToken, CancellationToken ct = default);
}
