using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Services;

public interface IDocumentProcessInfoService
{

    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);

    Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync();
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id);
    Task<DocumentProcessInfo> CreateDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo);
    Task<bool> DeleteDocumentProcessInfoAsync(Guid processId);
}
