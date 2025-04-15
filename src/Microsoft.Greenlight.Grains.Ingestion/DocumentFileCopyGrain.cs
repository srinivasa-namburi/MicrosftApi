// Microsoft.Greenlight.Grains.Ingestion/DocumentFileCopyGrain.cs

using Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

[Reentrant]
public class DocumentFileCopyGrain : Grain, IDocumentFileCopyGrain
{
    private readonly ILogger<DocumentFileCopyGrain> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    
    public DocumentFileCopyGrain(
        ILogger<DocumentFileCopyGrain> logger,
        [FromKeyedServices("blob-docing")] BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }
    
    public async Task CopyFilesFromBlobStorageAsync(
        string sourceContainerName, 
        string sourceFolderPath,
        string targetContainerName,
        string documentLibraryShortName,
        DocumentLibraryType documentLibraryType)
    {
        var orchestrationGrain = GrainFactory.GetGrain<IDocumentIngestionOrchestrationGrain>(this.GetPrimaryKey());
        
        try
        {
            var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(sourceContainerName);
            var targetContainerClient = _blobServiceClient.GetBlobContainerClient(targetContainerName);

            // Ensure target container exists
            await targetContainerClient.CreateIfNotExistsAsync();
            
            // Get list of blobs in the source folder
            var blobsPageable = sourceContainerClient.GetBlobsAsync(prefix: sourceFolderPath);
            var ingestPath = "ingest";
            var todayString = DateTime.Now.ToString("yyyy-MM-dd");
            
            await foreach (var blobPage in blobsPageable.AsPages())
            {
                foreach (var blob in blobPage.Values)
                {
                    try
                    {
                        var sourceBlobClient = sourceContainerClient.GetBlobClient(blob.Name);
                        var newBlobName = $"{ingestPath}/{todayString}/{blob.Name.Replace(sourceFolderPath + "/", "")}";
                        var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);
                        
                        // Copy the blob
                        await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
                        
                        // Delete the source blob
                        await sourceBlobClient.DeleteIfExistsAsync();
                        
                        // Log success
                        _logger.LogInformation(
                            documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary
                                ? "Document Process {documentProcess}: Copied blob {blobName} from {sourceContainer} to {targetContainer}"
                                : "Document Library {DocumentLibraryName}: Copied blob {blobName} from {sourceContainer} to {targetContainer}",
                            documentLibraryShortName, blob.Name, sourceContainerName, targetContainerName);
                        
                        // Notify orchestration grain about copied file
                        await orchestrationGrain.OnFileCopiedAsync(blob.Name, targetBlobClient.Uri.ToString());
                    }
                    catch (RequestFailedException exception)
                    {
                        // Handle case where blob already exists
                        if (exception.Status == 409)
                        {
                            var sourceBlobClient = sourceContainerClient.GetBlobClient(blob.Name);
                            await sourceBlobClient.DeleteIfExistsAsync();
                            
                            // Even if it exists, notify orchestration so we can process it
                            var newBlobName = $"{ingestPath}/{todayString}/{blob.Name.Replace(sourceFolderPath + "/", "")}";
                            var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);
                            await orchestrationGrain.OnFileCopiedAsync(blob.Name, targetBlobClient.Uri.ToString());
                        }
                        else
                        {
                            _logger.LogError(exception,
                                "Failed to copy blob {blobName} from {sourceContainer} to {targetContainer}",
                                blob.Name, sourceContainerName, targetContainerName);
                            
                            await orchestrationGrain.OnIngestionFailedAsync(
                                $"Failed to copy blob {blob.Name}: {exception.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Unexpected error copying blob {blobName} from {sourceContainer} to {targetContainer}",
                            blob.Name, sourceContainerName, targetContainerName);
                            
                        await orchestrationGrain.OnIngestionFailedAsync(
                            $"Unexpected error copying blob {blob.Name}: {ex.Message}");
                    }
                }
            }
            
            // If no files were found, this is still a successful operation - just with zero files
            _logger.LogInformation(
                "Completed copying files from {SourceContainer}/{SourceFolder} to {TargetContainer}",
                sourceContainerName, sourceFolderPath, targetContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Failed to process copy operation from {SourceContainer}/{SourceFolder} to {TargetContainer}",
                sourceContainerName, sourceFolderPath, targetContainerName);
                
            await orchestrationGrain.OnIngestionFailedAsync(
                $"Failed to process copy operation: {ex.Message}");
        }
    }
}