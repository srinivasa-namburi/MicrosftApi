// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO.FileStorage;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.FileStorage;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Abstract base class for file storage service implementations.
/// Provides common functionality for ExternalLinkAsset creation and database operations.
/// </summary>
public abstract class FileStorageServiceBase : IFileStorageService
{
    protected readonly ILogger _logger;
    protected readonly FileStorageSourceInfo _sourceInfo;
    protected readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStorageServiceBase"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="sourceInfo">Configuration for this storage source.</param>
    /// <param name="dbContextFactory">Database context factory for database operations.</param>
    protected FileStorageServiceBase(
        ILogger logger,
        FileStorageSourceInfo sourceInfo,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _sourceInfo = sourceInfo;
        _dbContextFactory = dbContextFactory;
    }

    // Abstract properties and methods that must be implemented by derived classes
    public abstract FileStorageProviderType ProviderType { get; }
    public abstract bool ShouldMoveFiles { get; }
    public abstract Guid SourceId { get; }
    public abstract string DefaultAutoImportFolder { get; }
    
    public abstract Task<IEnumerable<FileStorageItem>> DiscoverFilesAsync(string folderPath, string? filePattern = null, CancellationToken cancellationToken = default);
    public abstract Task<Stream> GetFileStreamAsync(string relativePath, CancellationToken cancellationToken = default);
    public abstract Task<Guid> RegisterFileDiscoveryAsync(string relativePath, string? fileHash = null, CancellationToken cancellationToken = default);
    public abstract Task<string> AcknowledgeFileAsync(string relativePath, string targetPath, CancellationToken cancellationToken = default);
    public abstract Task<bool> FileExistsAsync(string relativePath, CancellationToken cancellationToken = default);
    public abstract string GetFullPath(string relativePath);
    public abstract Task<string?> GetFileHashAsync(string relativePath, CancellationToken cancellationToken = default);
    public abstract Task<string> UploadFileAsync(string fileName, Stream stream, string? folderPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file properties for the specified file. Must be implemented by derived classes.
    /// </summary>
    /// <param name="relativePath">Relative path of the file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>File properties including content type and size.</returns>
    protected abstract Task<FileProperties> GetFilePropertiesAsync(string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves file information to the database and returns file access information.
    /// This method handles ExternalLinkAsset creation and provides access URLs.
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

            // Get file properties for additional metadata
            var properties = await GetFilePropertiesAsync(relativePath, cancellationToken);

            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var externalLinkAsset = new ExternalLinkAsset
            {
                Id = Guid.NewGuid(),
                Url = fullPath,
                FileName = relativePath, // Use full relative path for download, not just original filename
                FileHash = fileHash,
                MimeType = properties.ContentType ?? "application/octet-stream",
                FileSize = properties.Size,
                Description = $"Uploaded via {_sourceInfo.Name} storage source: {originalFileName}",
                FileStorageSourceId = _sourceInfo.Id
            };

            db.ExternalLinkAssets.Add(externalLinkAsset);
            await db.SaveChangesAsync(cancellationToken);

            // Create a proxied access URL using the ExternalLinkAsset ID
            var accessUrl = GetProxiedAssetUrl(externalLinkAsset.Id);

            return new FileUploadResult
            {
                RelativePath = relativePath,
                FullPath = fullPath,
                AccessUrl = accessUrl,
                ExternalLinkAssetId = externalLinkAsset.Id,
                FileHash = fileHash,
                FileSize = properties.Size,
                ContentType = properties.ContentType
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
    protected static string GetProxiedAssetUrl(Guid assetId)
    {
        // This creates a URL that routes through the FileController's DownloadExternalLinkAsset endpoint
        return $"/api/file/download/external-asset/{assetId}";
    }

    /// <summary>
    /// Represents file properties returned by storage providers.
    /// </summary>
    protected class FileProperties
    {
        public string? ContentType { get; set; }
        public long Size { get; set; }
    }
}