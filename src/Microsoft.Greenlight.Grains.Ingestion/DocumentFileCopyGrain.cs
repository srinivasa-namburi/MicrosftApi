// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Orleans.Concurrency;
using Microsoft.Greenlight.Shared.Models; 

namespace Microsoft.Greenlight.Grains.Ingestion;

[Reentrant]
public class DocumentFileCopyGrain : Grain, IDocumentFileCopyGrain
{
    private readonly ILogger<DocumentFileCopyGrain> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    
    public DocumentFileCopyGrain(
        ILogger<DocumentFileCopyGrain> logger,
        [FromKeyedServices("blob-docing")] BlobServiceClient blobServiceClient,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _dbContextFactory = dbContextFactory;
    }

    /// <inheritdoc cref="IDocumentFileCopyGrain.CopyFileAsync"/>
    public async Task<FileCopyResult> CopyFileAsync(Guid documentId)
    {
        _logger.LogInformation("[CopyFileAsync] Invoked for documentId={DocumentId}", documentId);

        IngestedDocument? entityToCopy;
        await using (var db = await _dbContextFactory.CreateDbContextAsync())
        {
            entityToCopy = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
        }

        if (entityToCopy == null)
        {
            _logger.LogWarning("IngestedDocument with Id {Id} not found in DB before copy.", documentId);
            return FileCopyResult.Fail("Document record not found in database.");
        }

        if (string.IsNullOrWhiteSpace(entityToCopy.FileName))
        {
            _logger.LogError("IngestedDocument with Id {Id} has no FileName.", documentId);
            return FileCopyResult.Fail("Document record is missing FileName.");
        }
        
        // Use container and folder path from the entity for consistency.
        string actualSourceContainerName = entityToCopy.Container;
        string targetContainerName = entityToCopy.Container; // Target container is same as source
        string actualSourceFolderPath = entityToCopy.FolderPath; 
        string actualSourceFileName = entityToCopy.FileName;   

        const string ingestPath = "ingest";
        var todayString = DateTime.Now.ToString("yyyy-MM-dd");

        // Construct the full path for the source blob. Azure SDK uses forward slashes.
        // Ensure no leading/trailing slashes cause issues.
        string fullSourceBlobPath = (string.IsNullOrEmpty(actualSourceFolderPath) ? actualSourceFileName : $"{actualSourceFolderPath.TrimEnd('/')}/{actualSourceFileName.TrimStart('/')}");
        // Normalize to remove any accidental double slashes
        fullSourceBlobPath = string.Join("/", fullSourceBlobPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
        
        // Use the simple file name part for the new blob name structure
        string simpleFileName = actualSourceFileName.Contains('/') ? actualSourceFileName.Substring(actualSourceFileName.LastIndexOf('/') + 1) : actualSourceFileName;
        string newBlobName = $"{ingestPath}/{todayString}/{simpleFileName}";

        try
        {
            var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(actualSourceContainerName);
            var targetContainerClient = _blobServiceClient.GetBlobContainerClient(targetContainerName);
            await targetContainerClient.CreateIfNotExistsAsync();

            var sourceBlobClient = sourceContainerClient.GetBlobClient(fullSourceBlobPath);

            if (!await sourceBlobClient.ExistsAsync())
            {
                _logger.LogWarning("Source blob {SourceBlobName} not found in container {SourceContainerName} for documentId {DocumentId}", 
                    fullSourceBlobPath, actualSourceContainerName, documentId);
                return FileCopyResult.Fail($"Source blob '{fullSourceBlobPath}' not found in container '{actualSourceContainerName}'.");
            }
            
            var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);

            _logger.LogInformation("Attempting to copy blob {SourceBlobFullPath} to {TargetBlobFullPath}", sourceBlobClient.Uri, targetBlobClient.Uri);
            
            var copyOperation = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

            BlobProperties properties;
            do
            {
                await Task.Delay(500); // Consider making delay configurable or exponential backoff for long copies
                properties = await targetBlobClient.GetPropertiesAsync();
                _logger.LogDebug("Polling copy status for {TargetBlobName}: {CopyStatus}", newBlobName, properties.CopyStatus);
            } while (properties.CopyStatus == CopyStatus.Pending);

            if (properties.CopyStatus == CopyStatus.Success)
            {
                _logger.LogInformation("Successfully copied blob {SourceBlobName} from {SourceContainer} to {TargetContainerAs}/{NewBlobName}", 
                    fullSourceBlobPath, actualSourceContainerName, targetContainerName, newBlobName);

                await sourceBlobClient.DeleteIfExistsAsync();
                _logger.LogInformation("Deleted source blob {SourceBlobName} from {SourceContainer}", fullSourceBlobPath, actualSourceContainerName);

                await using (var db = await _dbContextFactory.CreateDbContextAsync())
                {
                    // Re-fetch entity in this new context to update
                    var entityToUpdate = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                    if (entityToUpdate != null)
                    {
                        entityToUpdate.FinalBlobUrl = targetBlobClient.Uri.ToString();
                        entityToUpdate.IngestionState = IngestionState.FileCopied;
                        entityToUpdate.ModifiedUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                        _logger.LogInformation("Updated IngestedDocument {Id} with FinalBlobUrl and FileCopied state. New URL: {NewUrl}", 
                            documentId, entityToUpdate.FinalBlobUrl);
                    }
                    else
                    {
                        // This should ideally not happen if we found it at the start.
                        _logger.LogWarning("IngestedDocument with Id {Id} not found in DB for update after copy (second fetch).", documentId);
                        // Source was deleted, but DB update failed. This is a potential inconsistency.
                        return FileCopyResult.Fail("Couldn't find file record for update after successful copy and delete.");
                    }
                }
                return FileCopyResult.Ok();
            }
            else
            {
                _logger.LogError("Copy failed for blob {SourceBlobName} to {TargetBlobName}. Status: {Status}, Description: {Description}", 
                    fullSourceBlobPath, newBlobName, properties.CopyStatus, properties.CopyStatusDescription);
                return FileCopyResult.Fail($"Copy failed: {properties.CopyStatus} - {properties.CopyStatusDescription}");
            }
        }
        catch (RequestFailedException exception)
        {
            // Handle 409 Conflict: Target blob already exists.
            if (exception.Status == 409) 
            {
                _logger.LogWarning(exception, "Target blob already exists (Conflict/409) for {NewBlobName}. Assuming prior success, deleting source {SourceBlobName}.", newBlobName, fullSourceBlobPath);
                var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(actualSourceContainerName);
                var sourceBlobClient = sourceContainerClient.GetBlobClient(fullSourceBlobPath);
                await sourceBlobClient.DeleteIfExistsAsync();
                 _logger.LogInformation("Deleted source blob {SourceBlobName} after 409 conflict on target.", fullSourceBlobPath);
                // Consider re-fetching and updating the DB record here to ensure FinalBlobUrl and State are correct,
                // as the original copy might have succeeded but the DB update failed.
                // For now, returning Ok as per original logic if source is deleted.
                return FileCopyResult.Ok(); 
            }
            else
            {
                _logger.LogError(exception, "RequestFailedException while copying blob {SourceBlobName} to {TargetContainerName}/{NewBlobName}", 
                    fullSourceBlobPath, targetContainerName, newBlobName); 
                return FileCopyResult.Fail($"Failed to copy blob {fullSourceBlobPath}: {exception.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error copying blob {SourceBlobName} for documentId {DocumentId}", fullSourceBlobPath, documentId);
            return FileCopyResult.Fail($"Unexpected error copying blob {fullSourceBlobPath}: {ex.Message}");
        }
    }
}