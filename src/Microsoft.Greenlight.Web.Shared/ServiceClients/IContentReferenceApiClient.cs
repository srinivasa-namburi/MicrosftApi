using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients
{
    public interface IContentReferenceApiClient : IServiceClient
    {
        Task<List<ContentReferenceItemInfo>> GetAllReferencesAsync();
        Task<List<ContentReferenceItemInfo>> SearchReferencesAsync(string term);
        Task<ContentReferenceItemInfo> GetReferenceByIdAsync(Guid id, ContentReferenceType type);
        Task RefreshReferenceCacheAsync();
        Task<bool> RemoveReferenceAsync(Guid referenceId, Guid conversationId);
    }
}