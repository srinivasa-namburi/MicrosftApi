using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.Document;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IDocumentGenerationApiClient : IServiceClient
{
    Task<string?> GenerateDocumentAsync(GenerateDocumentDTO? generateDocumentDto);
    Task<GeneratedDocumentInfo?> GetDocumentAsync(string documentId);
    Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync();
    Task<bool> DeleteGeneratedDocumentAsync(string documentId);
    Task<Stream> ExportDocumentAsync(string documentId);
    Task<string?> GenerateExportDocumentLinkAsync(string? documentId);
    Task<string> GetExportDocumentLinkAsync(string documentId);
    Task<bool> StartDocumentValidationAsync(string documentId);
    Task<GeneratedDocumentInfo?> GetDocumentHeaderAsync(string documentId);
    Task<DocumentGenerationStatusInfo?> GetDocumentGenerationStatusAsync(string documentId);
    Task<DocumentGenerationFullStatusInfo?> GetDocumentGenerationFullStatusAsync(string documentId);
}

