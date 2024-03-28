using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IDocumentIngestionApiClient : IServiceClient
{
    Task<string?> IngestDocumentAsync(DocumentIngestionRequest? documentIngestionRequest);
    Task<string?> ReindexAllDocuments();
}