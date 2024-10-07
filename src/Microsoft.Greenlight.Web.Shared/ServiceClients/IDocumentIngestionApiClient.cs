using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IDocumentIngestionApiClient : IServiceClient
{
    Task<string?> IngestDocumentAsync(DocumentIngestionRequest? documentIngestionRequest);
    Task<string?> ReindexAllDocuments();
}
