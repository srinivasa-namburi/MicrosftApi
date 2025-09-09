// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Abstraction for file storage services that can discover, access, and acknowledge files.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Gets the provider type this service implements.
    /// </summary>
    FileStorageProviderType ProviderType { get; }

    /// <summary>
    /// Indicates whether this storage source is configured to move files upon acknowledgment.
    /// When false, discovery should occur at the root/container rather than an auto-import subfolder.
    /// </summary>
    bool ShouldMoveFiles { get; }

    /// <summary>
    /// The unique identifier of the underlying FileStorageSource.
    /// Used to correlate discovered files with acknowledgment records.
    /// </summary>
    Guid SourceId { get; }

    /// <summary>
    /// Gets the default auto-import folder name for this storage source.
    /// Implementations should return the configured AutoImportFolderName or a sensible default (e.g., "ingest-auto").
    /// </summary>
    string DefaultAutoImportFolder { get; }

    /// <summary>
    /// Discovers files in the specified folder path.
    /// </summary>
    /// <param name="folderPath">The folder path to scan for files.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.pdf").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered files.</returns>
    Task<IEnumerable<FileStorageItem>> DiscoverFilesAsync(string folderPath, string? filePattern = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a readable stream for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readable stream for the file.</returns>
    Task<Stream> GetFileStreamAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers that a file has been discovered without performing any movement.
    /// Creates or updates a FileAcknowledgmentRecord to track file discovery state.
    /// This is used during the discovery phase to establish file "newness" without moving files.
    /// </summary>
    /// <param name="relativePath">Relative path of the discovered file.</param>
    /// <param name="fileHash">Optional hash of the file content for change detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The FileAcknowledgmentRecord ID that was created or updated.</returns>
    Task<Guid> RegisterFileDiscoveryAsync(string relativePath, string? fileHash = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acknowledges that a file has been processed and takes appropriate action.
    /// For blob storage: moves/copies the file to a final location.
    /// For local file system: updates internal tracking without moving the file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file to acknowledge.</param>
    /// <param name="targetPath">Target path for the acknowledged file (provider-specific).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final path/URL of the acknowledged file.</returns>
    Task<string> AcknowledgeFileAsync(string relativePath, string targetPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full URL or path for accessing a file via external systems.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <returns>Full URL or path for external access.</returns>
    string GetFullPath(string relativePath);

    /// <summary>
    /// Computes a hash for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hash of the file content.</returns>
    Task<string?> GetFileHashAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a file stream to the specified relative path within the auto-import folder.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="stream">The file content stream.</param>
    /// <param name="folderPath">Optional subfolder within the auto-import folder. Defaults to "ingest-auto".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The relative path of the uploaded file for future operations.</returns>
    Task<string> UploadFileAsync(string fileName, Stream stream, string? folderPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves file information to the database and returns file access information.
    /// Creates <see cref="Microsoft.Greenlight.Shared.Models.FileStorage.ExternalLinkAsset"/> records
    /// for the uploaded file, enabling API-proxied download URLs.
    /// </summary>
    /// <param name="relativePath">The relative path of the uploaded file.</param>
    /// <param name="originalFileName">The original file name provided by the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File access information including URLs and metadata.</returns>
    Task<FileUploadResult> SaveFileInfoAsync(string relativePath, string originalFileName, CancellationToken cancellationToken = default);
}
