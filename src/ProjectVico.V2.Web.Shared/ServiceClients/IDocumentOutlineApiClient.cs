using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IDocumentOutlineApiClient : IServiceClient
{
    // Interface methods for http calls to DocumentOutlineController

    Task<List<DocumentOutlineInfo>> GetDocumentOutlinesAsync();
    Task<DocumentOutlineInfo?> GetDocumentOutlineByIdAsync(Guid id);
    Task<DocumentOutlineInfo?> CreateDocumentOutlineAsync(DocumentOutlineInfo documentOutlineInfo);
    Task<DocumentOutlineInfo?> UpdateDocumentOutlineAsync(Guid id,
        DocumentOutlineChangeRequest documentOutlineChangeRequest);

}