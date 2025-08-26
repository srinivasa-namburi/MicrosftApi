using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Services;

/// <summary>
/// The interface for a service to manage document process information.
/// </summary>
public interface IDocumentProcessInfoService
{
    /// <summary>
    /// Gets the document process information by short name.
    /// </summary>
    /// <param name="shortName">The short name of the document process.</param>
    /// <returns>The document process information if found; otherwise, null.</returns>
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByShortNameAsync(string shortName);

    /// <summary>
    /// Gets the document processes by short name
    /// </summary>
    /// <param name="libraryId">The ID of the library.</param>
    /// <returns>A list of document process information.</returns>
    DocumentProcessInfo GetDocumentProcessInfoByShortName(string shortName);

    /// <summary>
    /// Gets a combined list of document process information.
    /// </summary>
    /// <returns>A list of document process information.</returns>
    Task<List<DocumentProcessInfo>> GetCombinedDocumentProcessInfoListAsync();

    /// <summary>
    /// Gets the document process information by ID.
    /// </summary>
    /// <param name="id">The ID of the document process.</param>
    /// <returns>The document process information if found; otherwise, null.</returns>
    Task<DocumentProcessInfo?> GetDocumentProcessInfoByIdAsync(Guid id);

    /// <summary>
    /// Creates a new document process information.
    /// </summary>
    /// <param name="documentProcessInfo">The document process information to create.</param>
    /// <returns>The created document process information.</returns>
    Task<DocumentProcessInfo> CreateDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo);

    /// <summary>
    /// Creates a minimal document process without default outline or prompt implementations.
    /// This is useful for import scenarios where custom content will be added separately.
    /// </summary>
    /// <param name="documentProcessInfo">The document process information to create.</param>
    /// <returns>The created document process information.</returns>
    Task<DocumentProcessInfo> CreateMinimalDocumentProcessInfoAsync(DocumentProcessInfo documentProcessInfo);

    /// <summary>
    /// Deletes the document process information by process ID.
    /// </summary>
    /// <param name="processId">The ID of the document process to delete.</param>
    /// <returns>True if the document process was deleted; otherwise, false.</returns>
    Task<bool> DeleteDocumentProcessInfoAsync(Guid processId);

    /// <summary>
    /// Gets the document processes by library ID.
    /// </summary>
    /// <param name="libraryId">The ID of the library.</param>
    /// <returns>A list of document process information.</returns>
    Task<List<DocumentProcessInfo>> GetDocumentProcessesByLibraryIdAsync(Guid libraryId);

    
}
