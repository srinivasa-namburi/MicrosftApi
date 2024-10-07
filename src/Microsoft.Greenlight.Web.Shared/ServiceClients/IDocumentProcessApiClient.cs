using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IDocumentProcessApiClient : IServiceClient
{
    Task<List<DocumentProcessInfo>> GetAllDocumentProcessInfoAsync();
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);
    Task<DocumentProcessInfo?> CreateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo? documentProcessInfo);

    Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId);
    Task<PromptInfo> GetPromptByIdAsync(Guid id);
    Task CreatePromptAsync(PromptInfo promptInfo);
    Task UpdatePromptAsync(PromptInfo promptInfo);
    Task UpdateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo? documentProcessInfo);
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid? id);
    Task<bool> DeleteDocumentProcessAsync(Guid processId);
    Task<DocumentProcessExportInfo?> ExportDocumentProcessByIdAsync(Guid processId);
}
