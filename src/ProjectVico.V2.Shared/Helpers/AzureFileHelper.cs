using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Helpers;

public class AzureFileHelper
{
    private readonly BlobServiceClient _blobServiceClient;

    public AzureFileHelper(BlobServiceClient blobServiceClient)
    {
        _blobServiceClient = blobServiceClient;
    }
    public async Task<string> UploadFileToBlobAsync(Stream stream, string fileName, string containerName, bool overwriteIfExists)
    {
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();
        
        var blobClient = container.GetBlobClient(fileName);

        // Upload the blob - overwrite if it already exists
        await blobClient.UploadAsync(stream, overwrite: overwriteIfExists);
        
        return blobClient.Uri.ToString();
    }

    public async Task<Stream?> GetFileAsStreamFromFullBlobUrlAsync(string fullBlobUrl)
    {

        // Sample URL string : https://vicodevwedocing.blob.core.windows.net/ingest-nrc/ingest/2024-02-24/ML13115A763.pdf
        // From this URL, we need to extract the container name and the blob path. The blob path should be everything after the container name.

        var url = new Uri(fullBlobUrl);
        var containerName = url.Segments[1].TrimEnd('/');
        var blobPath = fullBlobUrl.Replace(url.Scheme + "://" + url.Host + "/" + containerName + "/", "");
       
        // Remove the SAS token if it exists at the end of the blobPath
        if (blobPath.Contains("?"))
        {
            blobPath = blobPath.Substring(0, blobPath.IndexOf('?'));
        }

        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var blobClient = container.GetBlobClient(blobPath);

        var download = await blobClient.OpenReadAsync();
        return download;

    }

    public string GetProxiedBlobUrl(string blobUrl)
    {
        var fileDownloadActionUrl = "/api/file/download/";
        return fileDownloadActionUrl + "?fileUrl=" + blobUrl;
    }

}