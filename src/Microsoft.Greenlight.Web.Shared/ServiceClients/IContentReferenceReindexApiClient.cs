using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Grains.Shared.Contracts.State;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IContentReferenceReindexApiClient : IServiceClient
{
    Task StartAsync(ContentReferenceType type, string reason);
    Task<ContentReferenceReindexState?> GetStateAsync(ContentReferenceType type);
}

