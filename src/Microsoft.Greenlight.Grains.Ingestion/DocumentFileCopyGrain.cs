// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.FileStorage;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Ingestion;

/// <summary>
/// Grain responsible for moving an ingested document from its discovery location
/// to a canonical ingest path, updating DB state, and cleaning up the source file.
/// 
/// Behavior highlights
/// - Uses file storage service providers to support multiple storage types
/// - Generates a unique target name (ingest/yyyy-MM-dd/{documentId}/{leaf-file}) to avoid collisions
/// - On successful copy: first updates DB (FinalBlobUrl, FileCopied), then acknowledges source
/// - If source is already gone, attempts to heal state rather than failing immediately
/// - Validates that target file exists to prevent downstream 404s
/// </summary>
[Reentrant]
public class DocumentFileCopyGrain : Grain, IDocumentFileCopyGrain
{
    private readonly ILogger<DocumentFileCopyGrain> _logger;
    private readonly IFileStorageServiceFactory _fileStorageServiceFactory;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;

    /// <summary>
    /// Creates a new instance of <see cref="DocumentFileCopyGrain"/>.
    /// </summary>
    public DocumentFileCopyGrain(
        ILogger<DocumentFileCopyGrain> logger,
        IFileStorageServiceFactory fileStorageServiceFactory,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory)
    {
        _logger = logger;
        _fileStorageServiceFactory = fileStorageServiceFactory;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Copies the file for the specified document to the ingest area and updates DB state.
    /// This method is idempotent and self-healing for common race conditions.
    /// </summary>
    /// <param name="documentId">The document id.</param>
    /// <returns>A <see cref="FileCopyResult"/> indicating success or failure.</returns>
    public async Task<FileCopyResult> CopyFileAsync(Guid documentId)
    {
        _logger.LogDebug("[CopyFileAsync] Invoked for documentId={DocumentId}", documentId);

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

        // Fast-path: For DiscoveredForConsumer, skip any physical file operation and simply
        // mark as FileCopied so the pipeline continues through the processing queue.
        if (entityToCopy.IngestionState == IngestionState.DiscoveredForConsumer)
        {
            try
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync();
                var entity = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                if (entity == null)
                {
                    _logger.LogWarning("IngestedDocument with Id {Id} not found in DB during DiscoveredForConsumer fast-path.", documentId);
                    return FileCopyResult.Fail("Document record not found during fast-path.");
                }

                // For file-storage-sourced items, the OriginalDocumentUrl is already the canonical path.
                // If FinalBlobUrl is missing, default it to OriginalDocumentUrl.
                entity.FinalBlobUrl ??= entity.OriginalDocumentUrl;
                entity.IngestionState = IngestionState.FileCopied;
                entity.ModifiedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                _logger.LogDebug("[CopyFileAsync] DiscoveredForConsumer fast-path: set FileCopied for {DocumentId}", documentId);
                return FileCopyResult.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DiscoveredForConsumer fast-path for documentId {DocumentId}", documentId);
                return FileCopyResult.Fail($"Fast-path update failed: {ex.Message}");
            }
        }

        try
        {
            // Get the appropriate file storage service for this document
            IFileStorageService fileStorageService;
            
            // Check if this is a document library (any of the library types) or a document process
            // Note: PrimaryDocumentProcessLibrary is used for document processes, not libraries
            bool isDocumentLibrary = entityToCopy.DocumentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary ||
                                   entityToCopy.DocumentLibraryType == DocumentLibraryType.Reviews;

            if (isDocumentLibrary)
            {
                var name = entityToCopy.DocumentLibraryOrProcessName ?? string.Empty;
                var services = await _fileStorageServiceFactory.GetServicesForDocumentProcessOrLibraryAsync(
                    name, isDocumentLibrary: true);
                fileStorageService = services.FirstOrDefault()!;
            }
            else
            {
                var name = entityToCopy.DocumentLibraryOrProcessName ?? string.Empty;
                var services = await _fileStorageServiceFactory.GetServicesForDocumentProcessOrLibraryAsync(
                    name, isDocumentLibrary: false);
                fileStorageService = services.FirstOrDefault()!;
            }

            if (fileStorageService == null)
            {
                _logger.LogError("No file storage service found for {DocumentLibraryType} {Name}", 
                    entityToCopy.DocumentLibraryType, entityToCopy.DocumentLibraryOrProcessName);
                return FileCopyResult.Fail($"No file storage service configured for {entityToCopy.DocumentLibraryType} {entityToCopy.DocumentLibraryOrProcessName}");
            }

            // Build the source path based on the original location
            var sourcePath = string.IsNullOrEmpty(entityToCopy.FolderPath) 
                ? entityToCopy.FileName 
                : $"{entityToCopy.FolderPath.TrimEnd('/')}/{entityToCopy.FileName}";

            // Generate a unique target path to avoid collisions (used when moving)
            const string ingestPath = "ingest";
            var todayString = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var uniqueSubfolder = documentId.ToString("N");
            var targetPath = $"{ingestPath}/{todayString}/{uniqueSubfolder}/{entityToCopy.FileName}";

            _logger.LogDebug("Attempting to copy file from {SourcePath} to {TargetPath} using {ServiceType}",
                sourcePath, targetPath, fileStorageService.GetType().Name);

            // Check if source file exists
            var sourceExists = await fileStorageService.FileExistsAsync(sourcePath);
            if (!sourceExists)
            {
                _logger.LogWarning("Source file {SourcePath} not found for documentId {DocumentId}", sourcePath, documentId);

                // If FinalBlobUrl is already set, assume previous copy completed successfully
                if (!string.IsNullOrWhiteSpace(entityToCopy.FinalBlobUrl))
                {
                    _logger.LogDebug("FinalBlobUrl already set for {DocumentId}, assuming previous copy completed.", documentId);
                    return FileCopyResult.Ok();
                }

                // Check if target already exists from a previous run
                var targetExists = await fileStorageService.FileExistsAsync(targetPath);
                if (targetExists)
                {
                    var targetUrl = fileStorageService.GetFullPath(targetPath);
                    await using var dbHeal = await _dbContextFactory.CreateDbContextAsync();
                    var healEntity = await dbHeal.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                    if (healEntity != null)
                    {
                        healEntity.FinalBlobUrl = targetUrl;
                        healEntity.IngestionState = IngestionState.FileCopied;
                        healEntity.ModifiedUtc = DateTime.UtcNow;
                        await dbHeal.SaveChangesAsync();
                        _logger.LogDebug("Healed DB state to FileCopied for {DocumentId} using existing target.", documentId);
                        return FileCopyResult.Ok();
                    }
                }

                return FileCopyResult.Fail($"Source file '{sourcePath}' not found.");
            }

            // Check if target already exists
            var targetAlreadyExists = await fileStorageService.FileExistsAsync(targetPath);
            if (targetAlreadyExists)
            {
                _logger.LogWarning("Target file {TargetPath} already exists for documentId {DocumentId}. Healing DB state.", targetPath, documentId);
                
                // Heal DB to FileCopied using the existing target
                var targetUrl = fileStorageService.GetFullPath(targetPath);
                await using (var db = await _dbContextFactory.CreateDbContextAsync())
                {
                    var entityToUpdate = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                    if (entityToUpdate != null)
                    {
                        entityToUpdate.FinalBlobUrl = targetUrl;
                        entityToUpdate.IngestionState = IngestionState.FileCopied;
                        entityToUpdate.ModifiedUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                        _logger.LogDebug("Updated IngestedDocument {Id} with existing target URL: {Url}", documentId, targetUrl);
                    }
                }

                // Acknowledge the source file (which may delete or move it depending on the provider)
                try
                {
                    await fileStorageService.AcknowledgeFileAsync(sourcePath, targetPath);
                    _logger.LogDebug("Acknowledged source file {SourcePath} after finding existing target.", sourcePath);
                }
                catch (Exception ackEx)
                {
                    _logger.LogWarning(ackEx, "Failed to acknowledge source file {SourcePath}. Continuing.", sourcePath);
                }

                return FileCopyResult.Ok();
            }

            // Perform the acknowledgment/copy operation. If provider is in ack-only mode, the returned URL will be the source URL.
            var finalTargetUrl = await fileStorageService.AcknowledgeFileAsync(sourcePath, targetPath);

            // If provider moves files, verify the target exists; otherwise we accept the source URL
            if (fileStorageService.ShouldMoveFiles)
            {
                var finalTargetExists = await fileStorageService.FileExistsAsync(targetPath);
                if (!finalTargetExists)
                {
                    _logger.LogError("Acknowledge operation completed but target file {TargetPath} does not exist for documentId {DocumentId}", targetPath, documentId);
                    return FileCopyResult.Fail("File acknowledgment completed but target file was not found.");
                }
            }

            _logger.LogInformation("Successfully copied file from {SourcePath} to {TargetPath} for documentId {DocumentId}", 
                sourcePath, targetPath, documentId);

            // Update DB with new location
            await using (var db = await _dbContextFactory.CreateDbContextAsync())
            {
                var entityToUpdate = await db.IngestedDocuments.FirstOrDefaultAsync(x => x.Id == documentId);
                if (entityToUpdate == null)
                {
                    _logger.LogWarning("IngestedDocument with Id {Id} not found in DB for update after copy.", documentId);
                    return FileCopyResult.Fail("Couldn't find file record for update after successful copy.");
                }

                entityToUpdate.FinalBlobUrl = finalTargetUrl;
                entityToUpdate.IngestionState = IngestionState.FileCopied;
                entityToUpdate.ModifiedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                _logger.LogDebug("Updated IngestedDocument {Id} with FinalBlobUrl and FileCopied state. New URL: {NewUrl}",
                    documentId, entityToUpdate.FinalBlobUrl);
            }

            return FileCopyResult.Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error copying file for documentId {DocumentId}", documentId);
            return FileCopyResult.Fail($"Unexpected error copying file: {ex.Message}");
        }
    }
}
