using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IContentNodeApiClient : IServiceClient
{
    Task<ContentNodeInfo?> GetContentNodeAsync(string contentNodeId);
    Task<ContentNodeSystemItemInfo?> GetContentNodeSystemItemAsync(Guid contentNodeSystemItemId);
}
