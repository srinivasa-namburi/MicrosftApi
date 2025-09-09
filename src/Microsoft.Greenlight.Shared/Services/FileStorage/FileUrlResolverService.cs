// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.FileStorage;
using Microsoft.Greenlight.Shared.Services.Caching;

namespace Microsoft.Greenlight.Shared.Services.FileStorage;

/// <summary>
/// Service for dynamically resolving file URLs to proxied API endpoints.
/// Provides a centralized way to generate URLs for files from various storage sources.
/// </summary>
public class FileUrlResolverService : IFileUrlResolverService
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<FileUrlResolverService> _logger;
    private readonly IAppCache _cache;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static string DocKey(Guid id) => $"fileurl:doc:{id}";
    private static string VsKey(string indexName, string documentId) => $"fileurl:vs:{indexName}:{documentId}";
    private static string AckKey(Guid id) => $"fileurl:ack:{id}";
    private static string CrKey(Guid id) => $"fileurl:cr:{id}";

    public FileUrlResolverService(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        ILogger<FileUrlResolverService> logger,
        IAppCache cache)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _cache = cache;
    }

    /// <inheritdoc />
    public async Task<string> ResolveUrlAsync(IngestedDocument ingestedDocument, CancellationToken cancellationToken = default)
    {
        try
        {
            // First check if this document is associated with a FileStorageSource
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var acknowledgment = ingestedDocument.IngestedDocumentFileAcknowledgments?.FirstOrDefault();
            if (acknowledgment?.FileAcknowledgmentRecord?.FileStorageSource != null)
            {
                var url = await ResolveFileStorageSourceUrlAsync(db, acknowledgment.FileAcknowledgmentRecord, cancellationToken);
                // Cache by document id (and ack id) for faster subsequent lookups
                try { await _cache.SetAsync(DocKey(ingestedDocument.Id), url, CacheTtl); } catch { /* ignore */ }
                try { await _cache.SetAsync(AckKey(acknowledgment.FileAcknowledgmentRecordId), url, CacheTtl); } catch { /* ignore */ }
                return url;
            }

            // Fallback: try to load acknowledgments if not included
            var acknowledgmentFromDb = await db.IngestedDocumentFileAcknowledgments
                .Include(idfa => idfa.FileAcknowledgmentRecord)
                    .ThenInclude(far => far.FileStorageSource)
                .FirstOrDefaultAsync(idfa => idfa.IngestedDocumentId == ingestedDocument.Id, cancellationToken);

            if (acknowledgmentFromDb?.FileAcknowledgmentRecord?.FileStorageSource != null)
            {
                var url = await ResolveFileStorageSourceUrlAsync(db, acknowledgmentFromDb.FileAcknowledgmentRecord, cancellationToken);
                try { await _cache.SetAsync(DocKey(ingestedDocument.Id), url, CacheTtl); } catch { }
                try { await _cache.SetAsync(AckKey(acknowledgmentFromDb.FileAcknowledgmentRecordId), url, CacheTtl); } catch { }
                return url;
            }

            // Legacy: return the raw URL for non-FileStorageSource files
            var rawUrl = ingestedDocument.FinalBlobUrl ?? ingestedDocument.OriginalDocumentUrl;
            _logger.LogDebug("Using legacy raw URL for document {DocumentId}: {Url}", ingestedDocument.Id, rawUrl);
            try { await _cache.SetAsync(DocKey(ingestedDocument.Id), rawUrl, CacheTtl); } catch { }
            return rawUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL for IngestedDocument {DocumentId}", ingestedDocument.Id);
            // Fallback to raw URL
            return ingestedDocument.FinalBlobUrl ?? ingestedDocument.OriginalDocumentUrl;
        }
    }

    /// <inheritdoc />
    public async Task<string> ResolveUrlAsync(FileAcknowledgmentRecord acknowledgmentRecord, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = AckKey(acknowledgmentRecord.Id);
            return await _cache.GetOrCreateAsync(key, async ct =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                return await ResolveFileStorageSourceUrlAsync(db, acknowledgmentRecord, ct);
            }, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL for FileAcknowledgmentRecord {RecordId}", acknowledgmentRecord.Id);
            // Fallback to raw path
            return acknowledgmentRecord.FileStorageSourceInternalUrl;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ResolveUrlByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = DocKey(documentId);
            return await _cache.GetOrCreateAsync<string?>(key, async ct =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                var ingestedDocument = await db.IngestedDocuments
                    .Include(d => d.IngestedDocumentFileAcknowledgments)
                        .ThenInclude(idfa => idfa.FileAcknowledgmentRecord)
                            .ThenInclude(far => far.FileStorageSource)
                    .FirstOrDefaultAsync(d => d.Id == documentId, ct);

                if (ingestedDocument == null)
                {
                    _logger.LogWarning("IngestedDocument {DocumentId} not found", documentId);
                    return null;
                }

                return await ResolveUrlAsync(ingestedDocument, ct);
            }, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL by document ID {DocumentId}", documentId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ResolveUrlByVectorStoreIdAsync(string vectorStoreDocumentId, string indexName, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = VsKey(indexName, vectorStoreDocumentId);
            return await _cache.GetOrCreateAsync<string?>(key, async ct =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);
                var ingestedDocument = await db.IngestedDocuments
                    .Include(d => d.IngestedDocumentFileAcknowledgments)
                        .ThenInclude(idfa => idfa.FileAcknowledgmentRecord)
                            .ThenInclude(far => far.FileStorageSource)
                    .FirstOrDefaultAsync(d => d.VectorStoreDocumentId == vectorStoreDocumentId && 
                                             d.VectorStoreIndexName == indexName, ct);

                if (ingestedDocument == null)
                {
                    _logger.LogWarning("IngestedDocument with VectorStoreDocumentId {VectorStoreDocumentId} in index {IndexName} not found", 
                        vectorStoreDocumentId, indexName);
                    return null;
                }

                return await ResolveUrlAsync(ingestedDocument, ct);
            }, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL by vector store ID {VectorStoreDocumentId} in index {IndexName}", 
                vectorStoreDocumentId, indexName);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ResolveUrlForContentReferenceAsync(Guid contentReferenceItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var key = CrKey(contentReferenceItemId);
            return await _cache.GetOrCreateAsync<string?>(key, async ct =>
            {
                await using var db = await _dbContextFactory.CreateDbContextAsync(ct);

                var cr = await db.ContentReferenceItems.FirstOrDefaultAsync(x => x.Id == contentReferenceItemId, ct);
                if (cr == null)
                {
                    return null;
                }

                // Prefer FileAcknowledgment linkage when present
                var join = await db.Set<ContentReferenceFileAcknowledgment>()
                    .Include(j => j.FileAcknowledgmentRecord)
                        .ThenInclude(f => f.FileStorageSource)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.ContentReferenceItemId == contentReferenceItemId, ct);

                if (join?.FileAcknowledgmentRecord != null)
                {
                    return await ResolveFileStorageSourceUrlAsync(db, join.FileAcknowledgmentRecord, ct);
                }

                // ExternalLinkAsset fallback by source id
                if (cr.ReferenceType == Shared.Enums.ContentReferenceType.ExternalLinkAsset && cr.ContentReferenceSourceId.HasValue)
                {
                    var assetId = cr.ContentReferenceSourceId.Value;
                    // Route through FileController
                    return $"/api/file/download/external-asset/{assetId}";
                }

                // ExportedDocumentLink fallback by source id
                if (cr.ReferenceType == Shared.Enums.ContentReferenceType.ExternalFile && cr.ContentReferenceSourceId.HasValue)
                {
                    // We may not have an ack record yet; try to locate by URL/Blob container
                    var link = await db.ExportedDocumentLinks.FirstOrDefaultAsync(l => l.Id == cr.ContentReferenceSourceId.Value, ct);
                    if (link != null)
                    {
                        // Create a temporary ack in-memory object to produce a proxied URL without persisting
                        var source = await db.FileStorageSources.FirstOrDefaultAsync(s => s.ContainerOrPath == link.BlobContainer, ct);
                        if (source != null)
                        {
                            var tempAck = new FileAcknowledgmentRecord
                            {
                                Id = Guid.NewGuid(),
                                FileStorageSourceId = source.Id,
                                RelativeFilePath = link.FileName,
                                FileStorageSourceInternalUrl = link.AbsoluteUrl,
                                FileHash = link.FileHash,
                                AcknowledgedDate = DateTime.UtcNow
                            };
                            return await ResolveFileStorageSourceUrlAsync(db, tempAck, ct);
                        }
                        return link.AbsoluteUrl;
                    }
                }

                return null;
            }, CacheTtl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving URL for ContentReferenceItem {ContentReferenceItemId}", contentReferenceItemId);
            return null;
        }
    }

    /// <summary>
    /// Resolves a URL for a file from a FileStorageSource, creating or reusing an ExternalLinkAsset.
    /// </summary>
    private async Task<string> ResolveFileStorageSourceUrlAsync(
        DocGenerationDbContext db, 
        FileAcknowledgmentRecord acknowledgmentRecord, 
        CancellationToken cancellationToken)
    {
        // Try to find existing ExternalLinkAsset for this file
        var existingAsset = await db.ExternalLinkAssets
            .FirstOrDefaultAsync(ela => ela.FileStorageSourceId == acknowledgmentRecord.FileStorageSourceId && 
                                       ela.Url == acknowledgmentRecord.FileStorageSourceInternalUrl, cancellationToken);

        if (existingAsset != null)
        {
            _logger.LogDebug("Using existing ExternalLinkAsset {AssetId} for FileAcknowledgmentRecord {RecordId}", 
                existingAsset.Id, acknowledgmentRecord.Id);
            var url = $"/api/file/download/external-asset/{existingAsset.Id}";
            try { await _cache.SetAsync(AckKey(acknowledgmentRecord.Id), url, CacheTtl); } catch { }
            return url;
        }

        // Create new ExternalLinkAsset
        var newAsset = new ExternalLinkAsset
        {
            Id = Guid.NewGuid(),
            Url = acknowledgmentRecord.FileStorageSourceInternalUrl,
            FileName = acknowledgmentRecord.RelativeFilePath, // Use full relative path, not just filename
            FileHash = acknowledgmentRecord.FileHash,
            MimeType = GetMimeTypeFromFileName(System.IO.Path.GetFileName(acknowledgmentRecord.RelativeFilePath)),
            FileSize = 0, // We don't have size info readily available
            Description = $"Auto-generated for {acknowledgmentRecord.RelativeFilePath}",
            FileStorageSourceId = acknowledgmentRecord.FileStorageSourceId
        };

        db.ExternalLinkAssets.Add(newAsset);
        await db.SaveChangesAsync(cancellationToken);

        var proxiedUrl = $"/api/file/download/external-asset/{newAsset.Id}";
        _logger.LogDebug("Created new ExternalLinkAsset {AssetId} for FileAcknowledgmentRecord {RecordId} with URL {ProxiedUrl}", 
            newAsset.Id, acknowledgmentRecord.Id, proxiedUrl);
        try { await _cache.SetAsync(AckKey(acknowledgmentRecord.Id), proxiedUrl, CacheTtl); } catch { }
        return proxiedUrl;
    }

    /// <summary>
    /// Gets the MIME type for a file based on its extension.
    /// </summary>
    private static string GetMimeTypeFromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".txt" => "text/plain",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" => "image/tiff",
            ".svg" => "image/svg+xml",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream"
        };
    }
}
