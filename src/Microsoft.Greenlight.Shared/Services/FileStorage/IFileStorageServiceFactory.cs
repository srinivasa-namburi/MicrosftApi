// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Factory for creating file storage service instances based on configuration.
/// </summary>
public interface IFileStorageServiceFactory
{
    /// <summary>
    /// Creates a file storage service instance for the specified source.
    /// </summary>
    /// <param name="sourceInfo">File storage source configuration.</param>
    /// <returns>Configured file storage service instance.</returns>
    IFileStorageService CreateService(FileStorageSourceInfo sourceInfo);

    /// <summary>
    /// Gets the default file storage service (first configured source).
    /// </summary>
    /// <returns>Default file storage service instance.</returns>
    IFileStorageService GetDefaultService();

    /// <summary>
    /// Gets all available file storage services for a document process/library.
    /// </summary>
    /// <param name="documentProcessOrLibraryName">Name of the document process or library.</param>
    /// <param name="isDocumentLibrary">True for document library, false for document process.</param>
    /// <returns>Collection of file storage services.</returns>
    Task<IEnumerable<IFileStorageService>> GetServicesForDocumentProcessOrLibraryAsync(string documentProcessOrLibraryName, bool isDocumentLibrary);

    /// <summary>
    /// Gets a file storage service by its source ID.
    /// </summary>
    /// <param name="sourceId">The FileStorageSource ID.</param>
    /// <returns>File storage service instance, or null if not found.</returns>
    Task<IFileStorageService?> GetServiceBySourceIdAsync(Guid sourceId);
}