using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Dynamic.Services;

public interface IDocumentProcessInfoService
{
    List<DocumentProcessInfo> GetCombinedDocumentInfoList();
    Task<DocumentProcessInfo?> GetDocumentInfoByShortNameAsync(string shortName);
    
}