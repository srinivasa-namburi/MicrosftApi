using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

public interface IDocumentLibraryApiClient : IServiceClient
{
    Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync();
    Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id);
    Task<List<DocumentProcessInfo>> GetDocumentProcessesByLibraryIdAsync(Guid libraryId);
    Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId);
    Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName);
    Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo);
    Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo);
    Task<bool> DeleteDocumentLibraryAsync(Guid id);
    Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);
    Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);
}