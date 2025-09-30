// Copyright (c) Microsoft Corporation. All rights reserved.

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// File storage service implementation for Azure Blob Storage.
/// </summary>
public class BlobStorageFileStorageService : FileStorageServiceBase
{
    private readonly BlobServiceClient _blobServiceClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="BlobStorageFileStorageService"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The BlobServiceClient instance.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="sourceInfo">Configuration for this storage source.</param>
    /// <param name="dbContextFactory">Database context factory for acknowledgment tracking.</param>
    public BlobStorageFileStorageService(
        BlobServiceClient blobServiceClient,
        ILogger<BlobStorageFileStorageService> logger,
        FileStorageSourceInfo sourceInfo,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
        : base(logger, sourceInfo, dbContextFactory)
    {
        _blobServiceClient = blobServiceClient;
    }

    /// <summary>
    /// Gets the provider type this service implements.
    /// </summary>
    public override FileStorageProviderType ProviderType => FileStorageProviderType.BlobStorage;

    /// <summary>
    /// Whether files should be moved after acknowledgment.
    /// </summary>
    public override bool ShouldMoveFiles => _sourceInfo.ShouldMoveFiles;

    /// <summary>
    /// The ID of the FileStorageSource that backs this service.
    /// </summary>
    public override Guid SourceId => _sourceInfo.Id;

    /// <summary>
    /// Default auto-import folder for this blob storage source.
    /// </summary>
    public override string DefaultAutoImportFolder => _sourceInfo.AutoImportFolderName ?? "ingest-auto";

    /// <summary>
    /// Discovers files in the specified folder path.
    /// </summary>
    /// <param name="folderPath">The folder path to scan for files.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.pdf").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered files.</returns>
    public override async Task<IEnumerable<FileStorageItem>> DiscoverFilesAsync(string folderPath, string? filePattern = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);

            // Ensure container exists
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var items = new List<FileStorageItem>();
            var prefix = string.IsNullOrEmpty(folderPath) ? string.Empty : folderPath.TrimEnd('/') + "/";

            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                // Skip if file pattern is specified and doesn't match
                if (!string.IsNullOrEmpty(filePattern) && !IsFilePatternMatch(blobItem.Name, filePattern))
                {
                    continue;
                }

                // IMPORTANT: For blob storage sources, RelativeFilePath must be from the
                // container root (including any folderPath/prefix). Do NOT strip the prefix.
                // This aligns with LocalFileStorageService behavior and downstream expectations.
                var relativePath = blobItem.Name;

                var item = new FileStorageItem
                {
                    RelativeFilePath = relativePath,
                    FullPath = GetFullPath(blobItem.Name),
                    Size = blobItem.Properties.ContentLength ?? 0,
                    LastModified = blobItem.Properties.LastModified?.DateTime ?? DateTime.UtcNow,
                    ContentHash = blobItem.Properties.ContentHash != null ? Convert.ToBase64String(blobItem.Properties.ContentHash) : null,
                    MimeType = blobItem.Properties.ContentType
                };

                items.Add(item);
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering files in folder {FolderPath} for container {Container}", folderPath, _sourceInfo.ContainerOrPath);
            throw;
        }
    }

    /// <summary>
    /// Gets a readable stream for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readable stream for the file.</returns>
    public override async Task<Stream> GetFileStreamAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            var blobClient = containerClient.GetBlobClient(relativePath);

            return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file stream for {RelativePath} in container {Container}", relativePath, _sourceInfo.ContainerOrPath);
            throw;
        }
    }

    /// <summary>
    /// Registers that a file has been discovered without performing any movement.
    /// Creates or updates a FileAcknowledgmentRecord to track file discovery state.
    /// </summary>
    /// <param name="relativePath">Relative path of the discovered file.</param>
    /// <param name="fileHash">Optional hash of the file content for change detection.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The FileAcknowledgmentRecord ID that was created or updated.</returns>
    public override async Task<Guid> RegisterFileDiscoveryAsync(string relativePath, string? fileHash = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            var blobClient = containerClient.GetBlobClient(relativePath);
            
            // Get the full URL for the file
            var fullPath = blobClient.Uri.ToString();
            
            // Calculate hash if not provided
            if (string.IsNullOrEmpty(fileHash))
            {
                try
                {
                    fileHash = await GetFileHashAsync(relativePath, cancellationToken);
                }
                catch (Exception hashEx)
                {
                    _logger.LogWarning(hashEx, "Failed to calculate hash for {RelativePath}, proceeding without hash", relativePath);
                }
            }

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            // Check if a record already exists
            var existing = await db.FileAcknowledgmentRecords
                .FirstOrDefaultAsync(x => x.FileStorageSourceId == _sourceInfo.Id && 
                                         x.RelativeFilePath == relativePath, cancellationToken);
            
            if (existing == null)
            {
                // Create new discovery record
                var acknowledgment = new FileAcknowledgmentRecord
                {
                    FileStorageSourceId = _sourceInfo.Id,
                    RelativeFilePath = relativePath,
                    FileStorageSourceInternalUrl = fullPath,
                    FileHash = fileHash,
                    AcknowledgedDate = DateTime.UtcNow,
                    DisplayFileName = Path.GetFileName(relativePath)
                };
                db.FileAcknowledgmentRecords.Add(acknowledgment);
                await db.SaveChangesAsync(cancellationToken);
                
                _logger.LogDebug("Registered file discovery for {RelativePath} in container {Container}", 
                    relativePath, _sourceInfo.ContainerOrPath);
                    
                return acknowledgment.Id;
            }
            else
            {
                // Update existing record if hash changed
                if (!string.Equals(existing.FileHash, fileHash, StringComparison.Ordinal))
                {
                    existing.FileHash = fileHash;
                    existing.AcknowledgedDate = DateTime.UtcNow;
                    existing.ModifiedUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                    
                    _logger.LogDebug("Updated file discovery record for {RelativePath} with new hash", relativePath);
                }
                
                return existing.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering file discovery for {RelativePath} in container {Container}", 
                relativePath, _sourceInfo.ContainerOrPath);
            throw;
        }
    }

    /// <summary>
    /// Acknowledges that a file has been processed and takes appropriate action.
    /// For blob storage: moves/copies the file to a final location or just acknowledges based on configuration.
    /// </summary>
    /// <param name="relativePath">Relative path of the file to acknowledge.</param>
    /// <param name="targetPath">Target path for the acknowledged file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Final path/URL of the acknowledged file.</returns>
    public override async Task<string> AcknowledgeFileAsync(string relativePath, string targetPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var sourceBlobClient = containerClient.GetBlobClient(relativePath);
            var targetBlobClient = containerClient.GetBlobClient(targetPath);

            if (_sourceInfo.ShouldMoveFiles)
            {
                // Move behavior: copy to target location and then delete source
                _logger.LogDebug("Moving file from {SourcePath} to {TargetPath} in container {Container}",
                    relativePath, targetPath, _sourceInfo.ContainerOrPath);

                // Start server-side copy
                var copyOperation = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: cancellationToken);

                // Poll for completion
                BlobProperties properties;
                do
                {
                    await Task.Delay(500, cancellationToken);
                    properties = await targetBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                }
                while (properties.CopyStatus == CopyStatus.Pending);

                if (properties.CopyStatus != CopyStatus.Success)
                {
                    throw new InvalidOperationException($"Failed to copy blob from {relativePath} to {targetPath}. Status: {properties.CopyStatus}");
                }

                // Upsert acknowledgment record to reflect the FINAL location after move.
                // Compute the file hash before deleting the source to ensure availability.
                try
                {
                    var fileHash = await GetFileHashAsync(relativePath, cancellationToken);
                    await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                    // Look for existing record by source ID and either the old or new path
                    var existing = await db.FileAcknowledgmentRecords
                        .FirstOrDefaultAsync(x => x.FileStorageSourceId == _sourceInfo.Id && 
                            (x.RelativeFilePath == relativePath || x.RelativeFilePath == targetPath), cancellationToken);

                    if (existing == null)
                    {
                        var acknowledgment = new FileAcknowledgmentRecord
                        {
                            FileStorageSourceId = _sourceInfo.Id,
                            // Store the FINAL relative path after move
                            RelativeFilePath = targetPath,
                            // Store the FINAL URL after move
                            FileStorageSourceInternalUrl = targetBlobClient.Uri.ToString(),
                            FileHash = fileHash,
                            AcknowledgedDate = DateTime.UtcNow,
                            DisplayFileName = Path.GetFileName(relativePath)
                        };
                        db.FileAcknowledgmentRecords.Add(acknowledgment);
                    }
                    else
                    {
                        // Update to FINAL relative path and URL after move
                        existing.RelativeFilePath = targetPath;
                        existing.FileStorageSourceInternalUrl = targetBlobClient.Uri.ToString();
                        existing.FileHash = fileHash;
                        existing.AcknowledgedDate = DateTime.UtcNow;
                    }

                    await db.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    // Do not fail the move due to acknowledgment persistence issues; log and continue
                    _logger.LogWarning(ex, "Failed to persist acknowledgment for moved file {SourcePath} in container {Container}", relativePath, _sourceInfo.ContainerOrPath);
                }

                // Delete the source file after successful copy
                try
                {
                    await sourceBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                    _logger.LogDebug("Deleted source file {SourcePath} after successful move", relativePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete source file {SourcePath} after copy. Target file created successfully.", relativePath);
                }

                return targetBlobClient.Uri.ToString();
            }
            else
            {
                // Acknowledge-only behavior: upsert acknowledgment record but don't move the file
                _logger.LogDebug("Acknowledging file {SourcePath} without moving (acknowledge-only mode)", relativePath);

                await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

                var existing = await db.FileAcknowledgmentRecords
                    .FirstOrDefaultAsync(x => x.FileStorageSourceId == _sourceInfo.Id && x.RelativeFilePath == relativePath, cancellationToken);

                var fileHash = await GetFileHashAsync(relativePath, cancellationToken);
                if (existing == null)
                {
                    var acknowledgment = new FileAcknowledgmentRecord
                    {
                        FileStorageSourceId = _sourceInfo.Id,
                        RelativeFilePath = relativePath,
                        FileStorageSourceInternalUrl = sourceBlobClient.Uri.ToString(),
                        FileHash = fileHash,
                        AcknowledgedDate = DateTime.UtcNow,
                        DisplayFileName = Path.GetFileName(relativePath)
                    };
                    db.FileAcknowledgmentRecords.Add(acknowledgment);
                }
                else
                {
                    existing.FileStorageSourceInternalUrl = sourceBlobClient.Uri.ToString();
                    existing.FileHash = fileHash;
                    existing.AcknowledgedDate = DateTime.UtcNow;
                }

                await db.SaveChangesAsync(cancellationToken);
                return sourceBlobClient.Uri.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging file {RelativePath} with target {TargetPath}", relativePath, targetPath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    public override async Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            var blobClient = containerClient.GetBlobClient(relativePath);

            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists {RelativePath} in container {Container}", relativePath, _sourceInfo.ContainerOrPath);
            return false;
        }
    }

    /// <summary>
    /// Gets the full URL for accessing a file via external systems.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <returns>Full URL for external access.</returns>
    public override string GetFullPath(string relativePath)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
        var blobClient = containerClient.GetBlobClient(relativePath);
        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Computes a hash for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hash of the file content.</returns>
    public override async Task<string?> GetFileHashAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            using var stream = await GetFileStreamAsync(relativePath, cancellationToken);
            return stream.GenerateHashFromStreamAndResetStream();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash for file {RelativePath} in container {Container}", relativePath, _sourceInfo.ContainerOrPath);
            return null;
        }
    }

    /// <summary>
    /// Uploads a file stream to the specified relative path within the auto-import folder.
    /// </summary>
    /// <param name="fileName">The name of the file to upload.</param>
    /// <param name="stream">The file content stream.</param>
    /// <param name="folderPath">Optional subfolder within the auto-import folder. Defaults to "ingest-auto".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The relative path of the uploaded file for future operations.</returns>
    public override async Task<string> UploadFileAsync(string fileName, Stream stream, string? folderPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the specified folder path or default to the auto-import folder
            var targetFolder = folderPath ?? _sourceInfo.AutoImportFolderName ?? "ingest-auto";

            // Generate a unique blob name to avoid conflicts
            var blobName = Guid.NewGuid() + Path.GetExtension(fileName);
            var relativePath = $"{targetFolder}/{blobName}";

            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var blobClient = containerClient.GetBlobClient(relativePath);

            _logger.LogDebug("Uploading file {FileName} as {BlobName} to container {Container}/{Folder}",
                fileName, blobName, _sourceInfo.ContainerOrPath, targetFolder);

            // Upload the blob with overwrite enabled
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to container {Container}", fileName, _sourceInfo.ContainerOrPath);
            throw;
        }
    }

    /// <summary>
    /// Gets file properties for the specified blob.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File properties including content type and size.</returns>
    protected override async Task<FileProperties> GetFilePropertiesAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_sourceInfo.ContainerOrPath);
            var blobClient = containerClient.GetBlobClient(relativePath);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return new FileProperties
            {
                ContentType = properties.Value.ContentType,
                Size = properties.Value.ContentLength
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file properties for {RelativePath} in container {Container}", relativePath, _sourceInfo.ContainerOrPath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file name matches the specified pattern.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
    /// <param name="pattern">The pattern to match against (e.g., "*.pdf").</param>
    /// <returns>True if the file matches the pattern, false otherwise.</returns>
    private static bool IsFilePatternMatch(string fileName, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern == "*")
        {
            return true;
        }

        // Simple pattern matching - only supports wildcards at the beginning or end
        if (pattern.StartsWith("*."))
        {
            var extension = pattern.Substring(2);
            return fileName.EndsWith($".{extension}", StringComparison.OrdinalIgnoreCase);
        }

        if (pattern.EndsWith("*"))
        {
            var prefix = pattern.Substring(0, pattern.Length - 1);
            return fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(fileName, pattern, StringComparison.OrdinalIgnoreCase);
    }
}
