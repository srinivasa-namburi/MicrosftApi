using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IDocumentGenerationApiClient : IServiceClient
{
    Task<string?> GenerateDocumentAsync(GenerateDocumentDTO? generateDocumentDto);
    Task<GeneratedDocument?> GetDocumentAsync(string documentId);
    Task<List<GeneratedDocumentListItem>> GetGeneratedDocumentsAsync();
    Task<bool> DeleteGeneratedDocumentAsync(string documentId);
    Task<Stream> ExportDocumentAsync(string documentId);
    Task<string> ExportDocumentLinkAsync(string documentId);
    Task<string> GetExportDocumentLinkAsync(string documentId);
}

