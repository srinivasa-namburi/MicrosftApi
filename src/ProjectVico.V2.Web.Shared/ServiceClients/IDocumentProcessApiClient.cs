using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IDocumentProcessApiClient : IServiceClient
{
    Task<List<DocumentProcessInfo>> GetAllDocumentProcessInfoAsync();
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);
    Task<DocumentProcessInfo?> CreateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo documentProcessInfo);

    Task<List<PromptInfo>> GetPromptsByProcessIdAsync(Guid processId);
    Task<PromptInfo> GetPromptByIdAsync(Guid id);
    Task CreatePromptAsync(PromptInfo promptInfo);
    Task UpdatePromptAsync(PromptInfo promptInfo);
    Task DeletePromptAsync(Guid promptId);
}