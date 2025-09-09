// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Contracts.Requests.FileStorage;
using Microsoft.Greenlight.Shared.Enums; // Added for ContentReferenceType

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client interface for managing file storage sources.
/// </summary>
public interface IFileStorageSourceApiClient
{
    /// <summary>
    /// Gets all file storage sources.
    /// </summary>
    /// <returns>A list of file storage source information.</returns>
    Task<List<FileStorageSourceInfo>> GetAllFileStorageSourcesAsync();

    /// <summary>
    /// Gets a specific file storage source by ID.
    /// </summary>
    /// <param name="id">The ID of the file storage source.</param>
    /// <returns>The file storage source information if found.</returns>
    Task<FileStorageSourceInfo?> GetFileStorageSourceByIdAsync(Guid id);

    /// <summary>
    /// Gets file storage sources for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>A list of file storage sources associated with the process.</returns>
    Task<List<FileStorageSourceInfo>> GetFileStorageSourcesByProcessIdAsync(Guid processId);

    /// <summary>
    /// Gets file storage sources for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>A list of file storage sources associated with the library.</returns>
    Task<List<FileStorageSourceInfo>> GetFileStorageSourcesByLibraryIdAsync(Guid libraryId);

    /// <summary>
    /// Creates a new file storage source.
    /// </summary>
    /// <param name="request">The request containing file storage source information to create.</param>
    /// <returns>The created file storage source information.</returns>
    Task<FileStorageSourceInfo> CreateFileStorageSourceAsync(CreateFileStorageSourceRequest request);

    /// <summary>
    /// Updates an existing file storage source.
    /// </summary>
    /// <param name="request">The request containing updated file storage source information.</param>
    /// <returns>The updated file storage source information.</returns>
    Task<FileStorageSourceInfo> UpdateFileStorageSourceAsync(UpdateFileStorageSourceRequest request);

    /// <summary>
    /// Deletes a file storage source.
    /// </summary>
    /// <param name="id">The ID of the file storage source to delete.</param>
    /// <returns>A task representing the operation.</returns>
    Task DeleteFileStorageSourceAsync(Guid id);

    /// <summary>
    /// Associates a file storage source with a document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>A task representing the operation.</returns>
    Task AssociateSourceWithProcessAsync(Guid processId, Guid sourceId);

    /// <summary>
    /// Disassociates a file storage source from a document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>A task representing the operation.</returns>
    Task DisassociateSourceFromProcessAsync(Guid processId, Guid sourceId);

    /// <summary>
    /// Associates a file storage source with a document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>A task representing the operation.</returns>
    Task AssociateSourceWithLibraryAsync(Guid libraryId, Guid sourceId);

    /// <summary>
    /// Disassociates a file storage source from a document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <returns>A task representing the operation.</returns>
    Task DisassociateSourceFromLibraryAsync(Guid libraryId, Guid sourceId);

    /// <summary>
    /// Gets file storage source associations for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>A list of file storage source associations for the process.</returns>
    Task<List<DocumentProcessFileStorageSourceInfo>> GetFileStorageSourceAssociationsByProcessIdAsync(Guid processId);

    /// <summary>
    /// Gets file storage source associations for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>A list of file storage source associations for the library.</returns>
    Task<List<DocumentLibraryFileStorageSourceInfo>> GetFileStorageSourceAssociationsByLibraryIdAsync(Guid libraryId);

    /// <summary>
    /// Updates the upload acceptance status and other properties for a document process file storage source association.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated association information.</returns>
    Task<DocumentProcessFileStorageSourceInfo> UpdateProcessSourceAssociationAsync(Guid processId, Guid sourceId, UpdateProcessSourceAssociationRequest request);

    /// <summary>
    /// Updates the upload acceptance status and other properties for a document library file storage source association.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <param name="sourceId">The ID of the file storage source.</param>
    /// <param name="request">The update request.</param>
    /// <returns>The updated association information.</returns>
    Task<DocumentLibraryFileStorageSourceInfo> UpdateLibrarySourceAssociationAsync(Guid libraryId, Guid sourceId, UpdateLibrarySourceAssociationRequest request);

    /// <summary>
    /// Gets the file storage source that accepts uploads for a specific document process.
    /// </summary>
    /// <param name="processId">The ID of the document process.</param>
    /// <returns>The file storage source that accepts uploads, or null if none is configured.</returns>
    Task<DocumentProcessFileStorageSourceInfo?> GetUploadSourceForProcessAsync(Guid processId);

    /// <summary>
    /// Gets the file storage source that accepts uploads for a specific document library.
    /// </summary>
    /// <param name="libraryId">The ID of the document library.</param>
    /// <returns>The file storage source that accepts uploads, or null if none is configured.</returns>
    Task<DocumentLibraryFileStorageSourceInfo?> GetUploadSourceForLibraryAsync(Guid libraryId);

    /// ContentReferenceType ↔ FileStorageSource mappings
    Task<List<ContentReferenceTypeStorageSourceMappingInfo>> GetAllContentReferenceTypeMappingsAsync();
    Task<List<ContentReferenceTypeStorageSourceMappingInfo>> GetContentReferenceTypeMappingsAsync(ContentReferenceType type);
    Task<ContentReferenceTypeStorageSourceMappingInfo> CreateContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId);
    Task<ContentReferenceTypeStorageSourceMappingInfo> UpdateContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId, int priority, bool isActive, bool acceptsUploads);
    Task DeleteContentReferenceTypeMappingAsync(ContentReferenceType type, Guid sourceId);

    #region Legacy method overloads for backward compatibility

    /// <summary>
    /// Creates a new file storage source (legacy overload).
    /// </summary>
    /// <param name="sourceInfo">The file storage source information to create.</param>
    /// <returns>The created file storage source information.</returns>
    [Obsolete("Use CreateFileStorageSourceAsync(CreateFileStorageSourceRequest) instead")]
    Task<FileStorageSourceInfo> CreateFileStorageSourceAsync(FileStorageSourceInfo sourceInfo);

    /// <summary>
    /// Updates an existing file storage source (legacy overload).
    /// </summary>
    /// <param name="id">The ID of the file storage source to update.</param>
    /// <param name="sourceInfo">The updated file storage source information.</param>
    /// <returns>The updated file storage source information.</returns>
    [Obsolete("Use UpdateFileStorageSourceAsync(UpdateFileStorageSourceRequest) instead")]
    Task<FileStorageSourceInfo> UpdateFileStorageSourceAsync(Guid id, FileStorageSourceInfo sourceInfo);

    #endregion
}
