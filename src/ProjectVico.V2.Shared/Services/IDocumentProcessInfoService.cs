using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Services;

public interface IDocumentProcessInfoService
{

    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);

    Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync();
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id);
    Task<DocumentProcessInfo> CreateDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo);
}