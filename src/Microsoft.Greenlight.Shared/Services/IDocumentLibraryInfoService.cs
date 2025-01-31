using Microsoft.Greenlight.Shared.Contracts.DTO.DocumentLibrary;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// An interface for a service to manage document libraries.
/// </summary>
public interface IDocumentLibraryInfoService
{
    /// <summary>
    /// Gets all document libraries.
    /// </summary>
    /// <returns>A list of document libraries.</returns>
    Task<List<DocumentLibraryInfo>> GetAllDocumentLibrariesAsync();

    /// <summary>
    /// Gets a document library by its ID.
    /// </summary>
    /// <param name="id">The ID of the document library.</param>
    /// <returns>The document library with the specified ID, or null if not found.</returns>
    Task<DocumentLibraryInfo?> GetDocumentLibraryByIdAsync(Guid id);

    /// <summary>
    /// Gets a document library by its short name.
    /// </summary>
    /// <param name="shortName">The short name of the document library.</param>
    /// <returns>The document library with the specified short name, or null if not found.</returns>
    Task<DocumentLibraryInfo?> GetDocumentLibraryByShortNameAsync(string shortName);

    /// <summary>
    /// Creates a new document library.
    /// </summary>
    /// <param name="documentLibraryInfo">The information of the document library to create.</param>
    /// <returns>The created document library.</returns>
    Task<DocumentLibraryInfo> CreateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo);

    /// <summary>
    /// Updates an existing document library.
    /// </summary>
    /// <param name="documentLibraryInfo">The information of the document library to update.</param>
    /// <returns>The updated document library.</returns>
    Task<DocumentLibraryInfo> UpdateDocumentLibraryAsync(DocumentLibraryInfo documentLibraryInfo);

    /// <summary>
    /// Deletes a document library by its ID.
    /// </summary>
    /// <param name="id">The ID of the document library to delete.</param>
    /// <returns>True if the document library was deleted, otherwise false.</returns>
    Task<bool> DeleteDocumentLibraryAsync(Guid id);

    /// <summary>
    /// Associates a document process with a document library.
    /// </summary>
    /// <param name="documentLibraryId">The ID of the document library.</param>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <returns> A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task AssociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);

    /// <summary>
    /// Disassociates a document process from a document library.
    /// </summary>
    /// <param name="documentLibraryId">The ID of the document library.</param>
    /// <param name="documentProcessId">The ID of the document process.</param>
    /// <returns> A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task DisassociateDocumentProcessAsync(Guid documentLibraryId, Guid documentProcessId);

    /// <summary>
    /// Gets document libraries by process ID.
    /// </summary>
    /// <param name="processId">The ID of the process.</param>
    /// <returns>A list of document libraries associated with the specified process ID.</returns>
    Task<List<DocumentLibraryInfo>> GetDocumentLibrariesByProcessIdAsync(Guid processId);

    /// <summary>
    /// Gets a document library by its index name.
    /// </summary>
    /// <param name="documentLibraryIndexName">The index name of the document library.</param>
    /// <returns>The document library with the specified index name, or null if not found.</returns>
    Task<DocumentLibraryInfo?> GetDocumentLibraryByIndexNameAsync(string documentLibraryIndexName);
}
