using ProjectVico.V2.Shared.Contracts;

namespace ProjectVico.V2.Shared.Services.DocumentInfo;

public interface IDocumentProcessInfoService
{

    Task<DocumentProcessInfo?> GetDocumentInfoByShortNameAsync(string shortName);

    Task<List<DocumentProcessInfo>> GetCombinedDocumentInfoListAsync();
}