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
        // Maybe turn it into a Url object first?

        var url = new Uri(fullBlobUrl);
        var containerName = url.Segments[1].TrimEnd('/');
        var blobPath = fullBlobUrl.Replace(url.Scheme + "://" + url.Host + "/" + containerName + "/", "");
        
        //var blobServiceBaseUrl = _blobServiceClient.Uri.ToString();
        //var blobPathWithContainer = fullBlobUrl.Replace(blobServiceBaseUrl + "/", "");

        //var containerName = blobPathWithContainer.Substring(0, blobPathWithContainer.IndexOf('/'));

        //var blobPath = blobPathWithContainer.Substring(blobPathWithContainer.IndexOf('/') + 1);
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

    public string GetTemporaryFileUrl(string originalUrl, TimeSpan accessTimeSpan, IngestionType ingestionType)
    {
       
        var blobServiceBaseUrl = _blobServiceClient.Uri.ToString();

        // Set blobPathWithContainer to be the originalUrl without the blobServiceBaseUrl
        var blobPathWithContainer = originalUrl.Remove(0, blobServiceBaseUrl.Length);
        // Remove any '/' at the beginning of the blobPathWithContainer
        blobPathWithContainer = blobPathWithContainer.TrimStart('/');

        var containerName = blobPathWithContainer.Substring(0, blobPathWithContainer.IndexOf('/'));
        var blobPath = blobPathWithContainer.Substring(blobPathWithContainer.IndexOf('/') + 1);
        
        
        // Generate a SAS token for the blob with the specified access time span
        var sasBuilder = new BlobSasBuilder(BlobContainerSasPermissions.Read, DateTimeOffset.UtcNow.Add(accessTimeSpan))
        {
            BlobContainerName = containerName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow
        };

        var blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobPath);
        var sasUrl = blobClient.GenerateSasUri(sasBuilder).ToString();

        return sasUrl;
    }

}