using ProjectVico.V2.Shared.Contracts;

namespace ProjectVico.V2.Shared.Services.DocumentInfo;

public interface IDocumentProcessInfoService
{
    List<DocumentProcessInfo> GetCombinedDocumentInfoList();
    Task<DocumentProcessInfo?> GetDocumentInfoByShortNameAsync(string shortName);
    
}