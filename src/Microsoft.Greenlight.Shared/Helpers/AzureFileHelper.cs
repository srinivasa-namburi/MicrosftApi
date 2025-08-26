using Azure;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.KernelMemory.Pipeline;
using System.Net;

namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Helper class for managing Azure Blob Storage operations.
/// </summary>
public class AzureFileHelper
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<AzureFileHelper> _logger;


    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFileHelper"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The BlobServiceClient instance.</param>
    /// <param name="dbContextFactory">Database Context factory</param>
    public AzureFileHelper(
        [FromKeyedServices("blob-docing")] BlobServiceClient blobServiceClient,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<AzureFileHelper> logger)
    {
        _blobServiceClient = blobServiceClient;
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether a blob container exists.
    /// </summary>
    /// <param name="containerName">Container name to check.</param>
    /// <returns>True if the container exists; otherwise false.</returns>
    public virtual async Task<bool> ContainerExistsAsync(string containerName)
    {
        if (string.IsNullOrWhiteSpace(containerName))
        {
            return false;
        }
        try
        {
            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            var exists = await container.ExistsAsync().ConfigureAwait(false);
            return exists.Value;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to query container existence for {Container}", containerName);
            return false;
        }
    }

    /// <summary>
    /// Uploads a file to Azure Blob Storage.
    /// </summary>
    /// <param name="stream">The file stream.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="overwriteIfExists">Whether to overwrite the file if it exists.</param>
    /// <returns>The URL of the uploaded file.</returns>
    public virtual async Task<string> UploadFileToBlobAsync(Stream stream, string fileName, string containerName,
        bool overwriteIfExists)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var blobClient = container.GetBlobClient(fileName);

        // Upload the blob - overwrite if it already exists
        await blobClient.UploadAsync(stream, overwrite: overwriteIfExists);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Saves file information to the database with file hash for deduplication.
    /// </summary>
    /// <param name="absoluteUrl">The absolute URL of the file.</param>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="generatedDocumentId">The ID of the generated document.</param>
    /// <returns>The saved ExportedDocumentLink entity.</returns>
    public virtual async Task<ExportedDocumentLink> SaveFileInfoAsync(string absoluteUrl, string containerName,
        string fileName, Guid? generatedDocumentId = null)
    {
        var dbContext = await _dbContextFactory.CreateDbContextAsync();

        var documentType = containerName switch
        {
            "document-export" => FileDocumentType.ExportedDocument,
            "document-assets" => FileDocumentType.DocumentAsset,
            "reviews" => FileDocumentType.Review,
            "temporary-references" => FileDocumentType.TemporaryReferenceFile,
            _ => FileDocumentType.ExportedDocument
        };

        // Calculate file hash by retrieving the file and computing its hash
        string? fileHash = null;
        try
        {
            // Get the file stream using the existing method - wrap with both null-safety and try-catch
            var fileStream = await GetFileAsStreamFromFullBlobUrlAsync(absoluteUrl);
            if (fileStream != null)
            {
                try
                {
                    // Use the StreamExtensions helper to generate the hash
                    fileHash = fileStream.GenerateHashFromStreamAndResetStream();
                }
                catch (Exception ex)
                {
                    // Log but continue - we'll just save without the hash
                    _logger.LogError($"Error calculating hash for {fileName}: {ex.Message}");
                }
            }
            else
            {
                _logger.LogWarning($"Warning: Could not retrieve file stream for {absoluteUrl}");
            }
        }
        catch (Exception ex)
        {
            // Log but continue - the file hash is optional
            _logger.LogError($"Error calculating file hash for {fileName}: {ex.Message}");
        }

        var entityEntry = await dbContext.ExportedDocumentLinks.AddAsync(new ExportedDocumentLink
        {
            GeneratedDocumentId = generatedDocumentId,
            AbsoluteUrl = absoluteUrl,
            BlobContainer = containerName,
            Created = DateTimeOffset.UtcNow,
            FileName = fileName,
            MimeType = new MimeTypesDetection().GetFileType(fileName),
            Type = documentType,
            FileHash = fileHash // Set the calculated hash - which may be null
        });

        await dbContext.SaveChangesAsync();

        return entityEntry.Entity;
    }

    /// <summary>
    /// Generates a hash for a file in blob storage given its absolute URL.
    /// </summary>
    /// <param name="absoluteUrl">The absolute URL of the file in blob storage.</param>
    /// <returns>The file hash as a string, or null if the file could not be hashed.</returns>
    public virtual async Task<string?> GenerateFileHashFromBlobUrlAsync(string absoluteUrl)
    {
        try
        {
            var fileStream = await GetFileAsStreamFromFullBlobUrlAsync(absoluteUrl);
            if (fileStream != null)
            {
                try
                {
                    return fileStream.GenerateHashFromStreamAndResetStream();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error calculating hash for {absoluteUrl}: {ex.Message}");
                    return null;
                }
            }
            else
            {
                _logger.LogWarning($"Warning: Could not retrieve file stream for {absoluteUrl}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error calculating file hash for {absoluteUrl}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Retrieves a file as a stream from a full blob URL.
    /// </summary>
    /// <param name="fullBlobUrl">The full URL of the blob.</param>
    /// <returns>The file stream.</returns>
    public virtual async Task<Stream?> GetFileAsStreamFromFullBlobUrlAsync(string fullBlobUrl)
    {
        var url = new Uri(fullBlobUrl);
        var containerName = url.Segments[1].TrimEnd('/');
        var blobPath = fullBlobUrl.Replace(url.Scheme + "://" + url.Host + "/" + containerName + "/", "");

        // Url Decode the blob path
        blobPath = WebUtility.UrlDecode(blobPath);

        // Remove the SAS token if it exists at the end of the blobPath
        if (blobPath.Contains('?'))
        {
            blobPath = blobPath.Substring(0, blobPath.IndexOf('?'));
        }

        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = container.GetBlobClient(blobPath);

        var download = await blobClient.OpenReadAsync();
        return download;
    }

    /// <summary>
    /// Retrieves a file as a stream from a container and blob name.
    /// </summary>
    /// <param name="container">The name of the container.</param>
    /// <param name="blobName">The name of the blob.</param>
    /// <returns>The file stream.</returns>
    public async Task<Stream?> GetFileAsStreamFromContainerAndBlobName(string container, string blobName)
    {
        // If the blobName contains the container name at the beginning, remove it
        if (blobName.StartsWith(container + "/"))
        {
            blobName = blobName.Replace(container + "/", "");
        }

        // If the blobName is a full URL, use the GetFileAsStreamFromFullBlobUrlAsync method
        if (blobName.Contains("http"))
        {
            return await GetFileAsStreamFromFullBlobUrlAsync(blobName);
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(container);
        var blobClient = containerClient.GetBlobClient(blobName);

        var download = await blobClient.OpenReadAsync();
        return download;
    }

    /// <summary>
    /// Gets a proxied URL for a blob.
    /// </summary>
    /// <param name="blobUrl">The URL of the blob.</param>
    /// <returns>The proxied URL.</returns>
    public string GetProxiedBlobUrl(string blobUrl)
    {
        var fileDownloadActionUrl = "/api/file/download/";

        // URL encode the blob URL
        blobUrl = WebUtility.UrlEncode(blobUrl);
        return fileDownloadActionUrl + blobUrl;
    }

    /// <summary>
    /// Gets a proxied URL for an asset blob.
    /// </summary>
    /// <param name="assetIdString">The asset ID as a string.</param>
    /// <returns>The proxied URL.</returns>
    public string GetProxiedAssetBlobUrl(string assetIdString)
    {
        var fileDownloadActionUrl = "/api/file/download/asset/";

        // URL encode the blob URL
        assetIdString = WebUtility.UrlEncode(assetIdString);
        return fileDownloadActionUrl + assetIdString;
    }

    /// <summary>
    /// Gets a proxied URL for an asset blob.
    /// </summary>
    /// <param name="assetIdGuid">The asset ID as a GUID.</param>
    /// <returns>The proxied URL.</returns>
    public string GetProxiedAssetBlobUrl(Guid assetIdGuid)
    {
        return GetProxiedAssetBlobUrl(assetIdGuid.ToString());
    }

    /// <summary>
    /// Deletes a blob from Azure Blob Storage.
    /// </summary>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="blobName">The name of the blob.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public virtual async Task DeleteBlobAsync(string containerName, string blobName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - deleting the blob is not critical
            Console.WriteLine($"Error deleting blob {blobName} from container {containerName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a writable stream directly to a blob.  Bytes are flushed to Azure
    /// as they are written; when the caller disposes the stream the upload is
    /// finalised.
    /// </summary>
    /// <param name="containerName">Target container.</param>
    /// <param name="blobName">Name / path of the blob.</param>
    /// <param name="overwrite">True = create or replace; false = fail if the blob already exists.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A writable <see cref="Stream"/>.</returns>
    public virtual async Task<Stream> OpenWriteStreamAsync(
        string containerName,
        string blobName,
        bool overwrite = true,
        CancellationToken cancellationToken = default)
    {
        // Make sure the container exists (no-op if it already does)
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blob = container.GetBlobClient(blobName);

        /*  Blob SDK notes
         *  ---------------
         *  - OpenWriteAsync returns a stream that uploads data in blocks under
         *    the hood; it is ideal for large, unknown-length payloads.
         *  - If you want to control block size, parallelism, etc. you can pass
         *    a BlobOpenWriteOptions instance (left at defaults here).
         */
        return await blob.OpenWriteAsync(
            overwrite: overwrite,
            options: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Opens a readable stream to an existing blob.  The caller reads it exactly
    /// like any other <see cref="Stream"/> – perfect for piping straight into
    /// Postgres COPY or for computing checksums.
    /// </summary>
    /// <param name="containerName">Name of the container.</param>
    /// <param name="blobName">Name / path of the blob inside the container.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>A readable <see cref="Stream"/>; throws <see cref="RequestFailedException"/>
    ///          if the blob does not exist.</returns>
    public virtual async Task<Stream> OpenReadStreamAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(blobName);

        /*  OpenReadAsync downloads on demand and supports random access if you
         *  pass allowModifications:false (the default).  Good for very large
         *  blobs because you never need the whole thing in memory or on disk.
         */
        return await blob.OpenReadAsync(
            options: null, // BlobOpenReadOptions
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get the storage account name from the blob service client.
    /// </summary>
    /// <returns></returns>
    public string GetStorageAccountName()
    {
        // Get the storage account name from the _blobServiceClient
        var uri = _blobServiceClient.Uri;
        var accountName = uri.Host.Split('.')[0];
        return accountName;

    }

    /// <summary>
    /// Get the blob service client from the helper
    /// </summary>
    /// <returns></returns>
    public BlobServiceClient GetBlobServiceClient()
    {
        return _blobServiceClient;
    }

    /// <summary>
    /// Deletes a blob container from Azure Blob Storage.
    /// </summary>
    /// <param name="containerName">The name of the container to delete.</param>
    /// <param name="ignoreNotFound">If true, do not throw when the container does not exist.</param>
    public virtual async Task DeleteBlobContainerAsync(string containerName, bool ignoreNotFound = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(containerName))
            {
                _logger.LogWarning("DeleteBlobContainerAsync called with empty container name");
                return;
            }

            var container = _blobServiceClient.GetBlobContainerClient(containerName);
            await container.DeleteIfExistsAsync();
            _logger.LogInformation("Deleted blob container {Container}", containerName);
        }
        catch (RequestFailedException ex) when (ignoreNotFound && ex.Status == 404)
        {
            _logger.LogDebug("Blob container {Container} not found during delete", containerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob container {Container}", containerName);
            throw;
        }
    }
}
