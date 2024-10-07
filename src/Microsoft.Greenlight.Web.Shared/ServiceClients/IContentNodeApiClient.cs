using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IContentNodeApiClient : IServiceClient
{
    Task<ContentNode?> GetContentNodeAsync(string contentNodeId);
}
