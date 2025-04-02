using System.Net;
using Azure.Storage.Blobs;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Helpers;

/// <summary>
/// Helper class for managing Azure Blob Storage operations.
/// </summary>
public class AzureFileHelper
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly DocGenerationDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureFileHelper"/> class.
    /// </summary>
    /// <param name="blobServiceClient">The BlobServiceClient instance.</param>
    /// <param name="dbContext">The database context.</param>
    public AzureFileHelper(BlobServiceClient blobServiceClient, DocGenerationDbContext dbContext)
    {
        _blobServiceClient = blobServiceClient;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Uploads a file to Azure Blob Storage.
    /// </summary>
    /// <param name="stream">The file stream.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="overwriteIfExists">Whether to overwrite the file if it exists.</param>
    /// <returns>The URL of the uploaded file.</returns>
    public virtual async Task<string> UploadFileToBlobAsync(Stream stream, string fileName, string containerName, bool overwriteIfExists)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();

        var blobClient = container.GetBlobClient(fileName);

        // Upload the blob - overwrite if it already exists
        await blobClient.UploadAsync(stream, overwrite: overwriteIfExists);

        return blobClient.Uri.ToString();
    }

    /// <summary>
    /// Saves file information to the database.
    /// </summary>
    /// <param name="absoluteUrl">The absolute URL of the file.</param>
    /// <param name="containerName">The name of the container.</param>
    /// <param name="fileName">The name of the file.</param>
    /// <param name="generatedDocumentId">The ID of the generated document.</param>
    /// <returns>The saved ExportedDocumentLink entity.</returns>
    public virtual async Task<ExportedDocumentLink> SaveFileInfoAsync(string absoluteUrl, string containerName, string fileName, Guid? generatedDocumentId = null)
    {
        var documentType = containerName switch
        {
            "document-export" => FileDocumentType.ExportedDocument,
            "document-assets" => FileDocumentType.DocumentAsset,
            "reviews" => FileDocumentType.Review,
            "temporary-references" => FileDocumentType.TemporaryReferenceFile, 
            _ => FileDocumentType.ExportedDocument
        };

        var entityEntry = await _dbContext.ExportedDocumentLinks.AddAsync(new ExportedDocumentLink
        {
            GeneratedDocumentId = generatedDocumentId,
            AbsoluteUrl = absoluteUrl,
            BlobContainer = containerName,
            Created = DateTimeOffset.UtcNow,
            FileName = fileName,
            MimeType = new MimeTypesDetection().GetFileType(fileName),
            Type = documentType
        });

        await _dbContext.SaveChangesAsync();

        return entityEntry.Entity;
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
}
