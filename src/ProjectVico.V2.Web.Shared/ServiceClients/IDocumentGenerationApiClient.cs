using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Web.Shared.ServiceClients;

public interface IDocumentGenerationApiClient : IServiceClient
{
    Task<string?> GenerateDocumentAsync(DocumentGenerationRequest? documentGenerationRequest);
    Task<GeneratedDocument?> GetDocumentAsync(string documentId);
    Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync();
    Task<bool> DeleteGeneratedDocumentAsync(string documentId);
}

