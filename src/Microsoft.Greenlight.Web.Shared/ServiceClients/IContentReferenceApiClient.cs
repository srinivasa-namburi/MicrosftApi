using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients
{
    public interface IContentReferenceApiClient : IServiceClient
    {
        Task<List<ContentReferenceItemInfo>> GetAllReferencesAsync();
        Task<List<ContentReferenceItemInfo>> SearchReferencesAsync(string term);
        Task<ContentReferenceItemInfo> GetReferenceByIdAsync(Guid id, ContentReferenceType type);
        Task<ContentReferenceItemInfo?> GetBySourceIdAsync(Guid sourceId, ContentReferenceType type);
        Task<List<ContentReferenceItemInfo>> GetAssistantReferenceListAsync(int top = 200);
        Task RefreshReferenceCacheAsync();
        Task<bool> RemoveReferenceAsync(Guid referenceId, Guid conversationId);
        Task<string?> GetDownloadUrlForContentReferenceAsync(Guid id);
    }
}
