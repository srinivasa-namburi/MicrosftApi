using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Services;

public interface IDocumentProcessInfoService
{

    Task<DocumentProcessInfo?> GetDocumentInfoByShortNameAsync(string shortName);

    Task<List<DocumentProcessInfo>> GetCombinedDocumentInfoListAsync();
    Task<DocumentProcessInfo?> GetDocumentInfoByIdAsync(Guid id);
}