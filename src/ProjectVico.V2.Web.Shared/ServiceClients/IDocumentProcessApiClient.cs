using ProjectVico.V2.Shared.Contracts;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IDocumentProcessApiClient : IServiceClient
{
    Task<List<DocumentProcessInfo>> GetAllDocumentProcessInfoAsync();
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);
    Task<DocumentProcessInfo?> CreateDynamicDocumentProcessDefinitionAsync(DocumentProcessInfo documentProcessInfo);

}