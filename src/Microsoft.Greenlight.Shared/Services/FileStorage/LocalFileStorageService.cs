// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// File storage service implementation for local file system.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly FileStorageSourceInfo _sourceInfo;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private string BaseRootPath => Path.Combine(_sourceInfo.FileStorageHost?.ConnectionString ?? string.Empty, _sourceInfo.ContainerOrPath ?? string.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalFileStorageService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="sourceInfo">Configuration for this storage source.</param>
    /// <param name="dbContextFactory">Database context factory for acknowledgment tracking.</param>
    public LocalFileStorageService(
        ILogger<LocalFileStorageService> logger,
        FileStorageSourceInfo sourceInfo,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _sourceInfo = sourceInfo;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Gets the provider type this service implements.
    /// </summary>
    public FileStorageProviderType ProviderType => FileStorageProviderType.LocalFileSystem;

    /// <summary>
    /// Whether files should be moved after acknowledgment.
    /// </summary>
    public bool ShouldMoveFiles => _sourceInfo.ShouldMoveFiles;

    /// <summary>
    /// The ID of the FileStorageSource that backs this service.
    /// </summary>
    public Guid SourceId => _sourceInfo.Id;

    /// <summary>
    /// Default auto-import folder for this local storage source.
    /// </summary>
    public string DefaultAutoImportFolder => _sourceInfo.AutoImportFolderName ?? "ingest-auto";

    /// <summary>
    /// Discovers files in the specified folder path.
    /// </summary>
    /// <param name="folderPath">The folder path to scan for files.</param>
    /// <param name="filePattern">Optional file pattern filter (e.g., "*.pdf").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of discovered files.</returns>
    public async Task<IEnumerable<FileStorageItem>> DiscoverFilesAsync(string folderPath, string? filePattern = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = BaseRootPath; // Combine host root with source-specific path
            var fullFolderPath = string.IsNullOrEmpty(folderPath)
                ? basePath
                : Path.Combine(basePath, folderPath);

            if (!Directory.Exists(fullFolderPath))
            {
                _logger.LogWarning("Directory {Directory} does not exist", fullFolderPath);
                return Enumerable.Empty<FileStorageItem>();
            }

            var searchPattern = string.IsNullOrEmpty(filePattern) ? "*" : filePattern;
            var files = Directory.GetFiles(fullFolderPath, searchPattern, SearchOption.TopDirectoryOnly);

            var items = new List<FileStorageItem>();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(basePath, file).Replace('\\', '/');

                var item = new FileStorageItem
                {
                    RelativeFilePath = relativePath,
                    FullPath = GetFullPath(relativePath),
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    ContentHash = await CalculateFileHashAsync(file, cancellationToken),
                    MimeType = GetMimeType(fileInfo.Extension)
                };

                items.Add(item);
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering files in folder {FolderPath} for root {Root}", folderPath, BaseRootPath);
            throw;
        }
    }

    /// <summary>
    /// Gets a readable stream for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Readable stream for the file.</returns>
    public Task<Stream> GetFileStreamAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            return Task.FromResult<Stream>(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file stream for {RelativePath} in root {Root}", relativePath, BaseRootPath);
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
    public async Task<Guid> RegisterFileDiscoveryAsync(string relativePath, string? fileHash = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            
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
                
                _logger.LogDebug("Registered file discovery for {RelativePath} in root {Root}", 
                    relativePath, BaseRootPath);
                    
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
            _logger.LogError(ex, "Error registering file discovery for {RelativePath} in root {Root}", 
                relativePath, BaseRootPath);
            throw;
        }
    }

    /// <summary>
    /// Acknowledges that a file has been processed by recording it in the database without moving the file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file to acknowledge.</param>
    /// <param name="targetPath">Target path for the acknowledged file (used for reference only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Full path of the acknowledged file (same as original).</returns>
    public async Task<string> AcknowledgeFileAsync(string relativePath, string targetPath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(relativePath);
            var fileHash = await GetFileHashAsync(relativePath, cancellationToken);

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            // Check if already acknowledged
            var existingAcknowledgment = await db.FileAcknowledgmentRecords
                .FirstOrDefaultAsync(x => x.FileStorageSourceId == _sourceInfo.Id &&
                                          x.RelativeFilePath == relativePath,
                                          cancellationToken);

            if (existingAcknowledgment == null)
            {
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
            }
            else
            {
                existingAcknowledgment.FileStorageSourceInternalUrl = fullPath;
                existingAcknowledgment.FileHash = fileHash;
                existingAcknowledgment.AcknowledgedDate = DateTime.UtcNow;
                existingAcknowledgment.DisplayFileName = Path.GetFileName(relativePath);
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Acknowledged file {RelativePath} in local storage", relativePath);

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging file {RelativePath} in root {Root}", relativePath, BaseRootPath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file exists at the specified path.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the file exists, false otherwise.</returns>
    public Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return Task.FromResult(File.Exists(fullPath));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if file exists {RelativePath} in root {Root}", relativePath, BaseRootPath);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Gets the full path for accessing a file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <returns>Full path for file access.</returns>
    public string GetFullPath(string relativePath)
    {
        return Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>
    /// Computes a hash for the specified file.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hash of the file content.</returns>
    public async Task<string?> GetFileHashAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return await CalculateFileHashAsync(fullPath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash for file {RelativePath} in root {Root}", relativePath, BaseRootPath);
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
    public async Task<string> UploadFileAsync(string fileName, Stream stream, string? folderPath = null, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use the specified folder path or default to the auto-import folder
            var targetFolder = folderPath ?? DefaultAutoImportFolder;

            // Generate a unique file name to avoid conflicts
            var uniqueFileName = Guid.NewGuid() + Path.GetExtension(fileName);
            var relativePath = Path.Combine(targetFolder, uniqueFileName).Replace('\\', '/');
            var fullPath = Path.Combine(BaseRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

            // Ensure the target directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _logger.LogDebug("Uploading file {FileName} as {UniqueFileName} to local path {FullPath}",
                fileName, uniqueFileName, fullPath);

            // Copy the stream to the target file
            using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            await stream.CopyToAsync(fileStream, cancellationToken);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file {FileName} to local storage {Root}", fileName, BaseRootPath);
            throw;
        }
    }

    /// <summary>
    /// Saves file information to the database and returns file access information.
    /// This method handles ExternalLinkAsset creation and provides access URLs.
    /// Note: For local file storage, file access is limited to internal system operations.
    /// </summary>
    /// <param name="relativePath">The relative path of the uploaded file.</param>
    /// <param name="originalFileName">The original file name provided by the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File access information including URLs and metadata.</returns>
    public async Task<FileUploadResult> SaveFileInfoAsync(string relativePath, string originalFileName, CancellationToken cancellationToken = default)
    {
        try
        {
            var fullPath = GetFullPath(relativePath);
            var fileHash = await GetFileHashAsync(relativePath, cancellationToken);

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var externalLinkAsset = new ExternalLinkAsset
            {
                Id = Guid.NewGuid(),
                Url = fullPath,
                FileName = relativePath, // Use full relative path for download, not just original filename
                FileHash = fileHash,
                MimeType = GetMimeType(Path.GetExtension(originalFileName)) ?? "application/octet-stream",
                FileSize = new FileInfo(fullPath).Length,
                Description = $"Uploaded via {_sourceInfo.Name} local storage source: {originalFileName}",
                FileStorageSourceId = _sourceInfo.Id
            };

            db.ExternalLinkAssets.Add(externalLinkAsset);
            await db.SaveChangesAsync(cancellationToken);

            // For local storage, we create a proxied access URL but note that direct file access may be limited
            var accessUrl = GetProxiedAssetUrl(externalLinkAsset.Id);

            return new FileUploadResult
            {
                RelativePath = relativePath,
                FullPath = fullPath,
                AccessUrl = accessUrl,
                ExternalLinkAssetId = externalLinkAsset.Id,
                FileHash = fileHash,
                FileSize = new FileInfo(fullPath).Length,
                ContentType = GetMimeType(Path.GetExtension(originalFileName))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file info for {RelativePath}", relativePath);
            throw;
        }
    }

    /// <summary>
    /// Gets a proxied URL for an asset using the ExternalLinkAsset ID.
    /// This method creates URLs that route through the application's file controller.
    /// </summary>
    /// <param name="assetId">The ExternalLinkAsset ID.</param>
    /// <returns>The proxied URL.</returns>
    private static string GetProxiedAssetUrl(Guid assetId)
    {
        // This creates a URL that routes through the FileController's DownloadExternalLinkAsset endpoint
        return $"/api/file/download/external-asset/{assetId}";
    }

    /// <summary>
    /// Calculates the hash of a file.
    /// </summary>
    /// <param name="filePath">Full path to the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hash of the file content.</returns>
    private Task<string?> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            string? hash = stream.GenerateHashFromStreamAndResetStream();
            return Task.FromResult<string?>(hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating hash for file {FilePath}", filePath);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    /// <param name="extension">File extension including the dot.</param>
    /// <returns>MIME type string.</returns>
    private static string? GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".xml" => "text/xml",
            ".json" => "application/json",
            ".csv" => "text/csv",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".zip" => "application/zip",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}