using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services;

public interface IDocumentLibraryInfoService
{
    Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync();
    Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id);
    Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName);
    Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo? documentLibraryInfo);
    Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo? documentLibraryInfo);
    Task<bool> DeleteDocumentLibraryAsync(Guid id);
    Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);
    Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);
    Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId);
    Task<DocumentLibraryInfo?> GetDocumentLibraryByIndexNameAsync(string documentLibraryIndexName);
}