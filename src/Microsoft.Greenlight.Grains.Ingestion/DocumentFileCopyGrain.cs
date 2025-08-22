// Copyright (c) Microsoft Corporation. All rights reserved.
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Orleans.Concurrency;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Grains.Ingestion;

/// <summary>
/// Grain responsible for moving an ingested document from its discovery location
/// to a canonical ingest path, updating DB state, and cleaning up the source blob.
/// 
/// Behavior highlights
/// - Resolves the source from OriginalDocumentUrl when available to avoid path reconstruction errors
/// - Generates a unique target name (ingest/yyyy-MM-dd/{documentId}/{leaf-file}) to avoid collisions
/// - On successful copy: first updates DB (FinalBlobUrl, FileCopied), then deletes source
/// - On 409 (already exists): heals DB to FileCopied and deletes source (with existence validation)
/// - If source is missing but target exists or FinalBlobUrl is set, heals and returns success (idempotent)
/// - Validates that FinalBlobUrl points at an existing blob to prevent downstream 404s
/// </summary>
[Reentrant]
public class DocumentFileCopyGrain : Grain, IDocumentFileCopyGrain
{
    private readonly ILogger<DocumentFileCopyGrain> _logger;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Creates a new instance of <see cref="DocumentFileCopyGrain"/>.
    /// </summary>
    public DocumentFileCopyGrain(
        ILogger<DocumentFileCopyGrain> logger,
        [FromKeyedServices("blob-docing")] BlobServiceClient blobServiceClient,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Copies the blob for the specified document to the ingest area and updates DB state.
    /// This method is idempotent and self-healing for common race conditions (e.g., 409 conflicts).
    /// Validates that the FinalBlobUrl we save actually exists.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    /// <returns>A <see cref="FileCopyResult"/> indicating success or failure.</returns>
    public async Task<FileCopyResult> CopyFileAsync(Guid documentId)
    {
        _logger.LogInformation("[CopyFileAsync] Invoked for documentId={DocumentId}", documentId);

        // Look up entity once to determine source and target
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

        // Resolve source directly from OriginalDocumentUrl when present to avoid nested-folder path issues.
        // Fallback to Container + FolderPath/FileName for legacy rows with no OriginalDocumentUrl.
        string sourceContainerName;
        string sourceBlobPath;
        try
        {
            if (!string.IsNullOrWhiteSpace(entityToCopy.OriginalDocumentUrl))
            {
                var sourceUri = new Uri(entityToCopy.OriginalDocumentUrl);
                // Container is the first path segment: https://account.blob.core.windows.net/{container}/{blobPath}
                sourceContainerName = sourceUri.Segments.Length > 1 ? sourceUri.Segments[1].TrimEnd('/') : entityToCopy.Container;

                // Blob path is the remainder after host and container
                var prefix = $"{sourceUri.Scheme}://{sourceUri.Host}/{sourceContainerName}/";
                sourceBlobPath = entityToCopy.OriginalDocumentUrl.Replace(prefix, string.Empty, StringComparison.OrdinalIgnoreCase);

                // Strip SAS if present
                var qIndex = sourceBlobPath.IndexOf('?', StringComparison.Ordinal);
                if (qIndex >= 0)
                {
                    sourceBlobPath = sourceBlobPath.Substring(0, qIndex);
                }

                // Decode any percent-encoding
                sourceBlobPath = WebUtility.UrlDecode(sourceBlobPath);
            }
            else
            {
                sourceContainerName = entityToCopy.Container;
                var folder = entityToCopy.FolderPath ?? string.Empty;
                var file = entityToCopy.FileName;
                var combined = string.IsNullOrEmpty(folder)
                    ? file
                    : $"{folder.TrimEnd('/')}/{file.TrimStart('/')}";
                // Normalize to avoid accidental double slashes
                sourceBlobPath = string.Join("/", combined.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OriginalDocumentUrl for {DocumentId}", documentId);
            return FileCopyResult.Fail("Invalid OriginalDocumentUrl for document.");
        }

        // Generate a unique, collision-free target name under the same container.
        // Using documentId ensures uniqueness across files with identical leaf names and across retries.
        const string ingestPath = "ingest";
        var todayString = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var simpleFileName = sourceBlobPath.Contains('/')
            ? sourceBlobPath[(sourceBlobPath.LastIndexOf('/') + 1)..]
            : sourceBlobPath;
        var uniqueSubfolder = documentId.ToString("N");
        var newBlobName = $"{ingestPath}/{todayString}/{uniqueSubfolder}/{simpleFileName}";

        try
        {
            var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(sourceContainerName);
            var targetContainerClient = _blobServiceClient.GetBlobContainerClient(entityToCopy.Container);
            await targetContainerClient.CreateIfNotExistsAsync();

            var sourceBlobClient = sourceContainerClient.GetBlobClient(sourceBlobPath);
            var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);

            // If the source is already gone, attempt to heal state rather than failing immediately.
            if (!await sourceBlobClient.ExistsAsync())
            {
                _logger.LogWarning("Source blob {SourceBlobName} not found in container {SourceContainerName} for documentId {DocumentId}",
                    sourceBlobPath, sourceContainerName, documentId);

                // If FinalBlobUrl is already set, assume previous copy completed successfully.
                if (!string.IsNullOrWhiteSpace(entityToCopy.FinalBlobUrl))
                {
                    _logger.LogInformation("FinalBlobUrl already set for {DocumentId}, assuming previous copy completed.", documentId);
                    return FileCopyResult.Ok();
                }

                // Otherwise, if the expected target exists, update DB and return success.
                if (await targetBlobClient.ExistsAsync())
                {
                    await using var dbHeal = await _dbContextFactory.CreateDbContextAsync();
                    var healEntity = await dbHeal.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                    if (healEntity != null)
                    {
                        healEntity.FinalBlobUrl = targetBlobClient.Uri.ToString();
                        healEntity.IngestionState = IngestionState.FileCopied;
                        healEntity.ModifiedUtc = DateTime.UtcNow;
                        await dbHeal.SaveChangesAsync();
                        _logger.LogInformation("Healed DB state to FileCopied for {DocumentId} using existing target.", documentId);
                        return FileCopyResult.Ok();
                    }
                }

                return FileCopyResult.Fail($"Source blob '{sourceBlobPath}' not found in container '{sourceContainerName}'.");
            }

            _logger.LogInformation("Attempting to copy blob {SourceBlobFullPath} to {TargetBlobFullPath}", sourceBlobClient.Uri, targetBlobClient.Uri);

            // Start server-side copy
            var _ = await targetBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);

            // Poll status; consider backoff for very large blobs in future if needed
            BlobProperties properties;
            do
            {
                await Task.Delay(500);
                properties = await targetBlobClient.GetPropertiesAsync();
                _logger.LogDebug("Polling copy status for {TargetBlobName}: {CopyStatus}", newBlobName, properties.CopyStatus);
            }
            while (properties.CopyStatus == CopyStatus.Pending);

            if (properties.CopyStatus == CopyStatus.Success)
            {
                // Validate that the target actually exists before writing FinalBlobUrl
                if (!await targetBlobClient.ExistsAsync())
                {
                    _logger.LogError("Copy reported success but target does not exist for {TargetBlob}", newBlobName);
                    return FileCopyResult.Fail("Copy reported success but target blob was not found.");
                }

                _logger.LogInformation("Successfully copied blob {SourceBlobName} from {SourceContainer} to {TargetContainer}/{NewBlobName}",
                    sourceBlobPath, sourceContainerName, entityToCopy.Container, newBlobName);

                // 1) Update DB so processing uses FinalBlobUrl even if source delete fails
                await using (var db = await _dbContextFactory.CreateDbContextAsync())
                {
                    var entityToUpdate = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                    if (entityToUpdate == null)
                    {
                        _logger.LogWarning("IngestedDocument with Id {Id} not found in DB for update after copy (second fetch).", documentId);
                        return FileCopyResult.Fail("Couldn't find file record for update after successful copy.");
                    }

                    entityToUpdate.FinalBlobUrl = targetBlobClient.Uri.ToString();
                    entityToUpdate.IngestionState = IngestionState.FileCopied;
                    entityToUpdate.ModifiedUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                    _logger.LogInformation("Updated IngestedDocument {Id} with FinalBlobUrl and FileCopied state. New URL: {NewUrl}",
                        documentId, entityToUpdate.FinalBlobUrl);
                }

                // 2) Delete the source to prevent reprocessing. Ignore delete errors.
                try
                {
                    await sourceBlobClient.DeleteIfExistsAsync();
                    _logger.LogInformation("Deleted source blob {SourceBlobName} from {SourceContainer}", sourceBlobPath, sourceContainerName);
                }
                catch (Exception delEx)
                {
                    _logger.LogWarning(delEx, "Failed to delete source blob {SourceBlobName} from {SourceContainer}. Continuing.", sourceBlobPath, sourceContainerName);
                }

                return FileCopyResult.Ok();
            }

            // Copy not successful
            _logger.LogError("Copy failed for blob {SourceBlobName} to {TargetBlobName}. Status: {Status}, Description: {Description}",
                sourceBlobPath, newBlobName, properties.CopyStatus, properties.CopyStatusDescription);
            return FileCopyResult.Fail($"Copy failed: {properties.CopyStatus} - {properties.CopyStatusDescription}");
        }
        catch (RequestFailedException exception)
        {
            // Handle conflict: target already exists (e.g., retried run or previous copy completed)
            if (exception.Status == 409)
            {
                var targetContainerClient = _blobServiceClient.GetBlobContainerClient(entityToCopy.Container);
                var targetBlobClient = targetContainerClient.GetBlobClient(newBlobName);

                // Validate existence before healing DB
                if (!await targetBlobClient.ExistsAsync())
                {
                    _logger.LogError("Received 409 for {TargetBlobName} but target does not exist.", newBlobName);
                    return FileCopyResult.Fail("Conflict reported but target blob not found.");
                }

                _logger.LogWarning(exception, "Target blob already exists (409) for {NewBlobName}. Healing DB and deleting source {SourceBlobName}.", newBlobName, sourceBlobPath);

                try
                {
                    // Heal DB to FileCopied using the known target
                    await using (var db = await _dbContextFactory.CreateDbContextAsync())
                    {
                        var entityToUpdate = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                        if (entityToUpdate != null)
                        {
                            entityToUpdate.FinalBlobUrl = targetBlobClient.Uri.ToString();
                            entityToUpdate.IngestionState = IngestionState.FileCopied;
                            entityToUpdate.ModifiedUtc = DateTime.UtcNow;
                            await db.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception healEx)
                {
                    _logger.LogWarning(healEx, "Failed to update DB after 409 for {DocumentId}.", documentId);
                }

                try
                {
                    var sourceContainerClient = _blobServiceClient.GetBlobContainerClient(sourceContainerName);
                    var sourceBlobClient = sourceContainerClient.GetBlobClient(sourceBlobPath);
                    await sourceBlobClient.DeleteIfExistsAsync();
                    _logger.LogInformation("Deleted source blob {SourceBlobName} after 409 conflict on target.", sourceBlobPath);
                }
                catch (Exception delEx)
                {
                    _logger.LogWarning(delEx, "Failed deleting source blob after 409 for {DocumentId}", documentId);
                }

                return FileCopyResult.Ok();
            }

            // Other storage errors
            _logger.LogError(exception, "RequestFailedException while copying blob {SourceBlobName} to {TargetContainerName}/{NewBlobName}",
                sourceBlobPath, entityToCopy.Container, newBlobName);
            return FileCopyResult.Fail($"Failed to copy blob {sourceBlobPath}: {exception.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error copying blob {SourceBlobName} for documentId {DocumentId}", sourceBlobPath, documentId);
            return FileCopyResult.Fail($"Unexpected error copying blob {sourceBlobPath}: {ex.Message}");
        }
    }
}