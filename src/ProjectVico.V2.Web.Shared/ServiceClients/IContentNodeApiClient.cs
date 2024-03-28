using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IContentNodeApiClient : IServiceClient
{
    Task<ContentNode?> GetContentNodeAsync(string contentNodeId);
}