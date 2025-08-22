// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

/// <summary>
/// One-time heavy maintenance job that migrates old filename-based vector-store IDs to canonical Base64Url IDs
/// for all document libraries and processes using SemanticKernelVectorStore. Intended to run once on startup and
/// then rarely (e.g., monthly) thereafter.
/// </summary>
[Reentrant]
public class VectorStoreIdFixGrain : Grain, IVectorStoreIdFixGrain
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<VectorStoreIdFixGrain> _logger;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly AzureFileHelper _fileHelper;

    public VectorStoreIdFixGrain(
        IServiceProvider sp,
        ILogger<VectorStoreIdFixGrain> logger,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        AzureFileHelper fileHelper)
    {
        _sp = sp;
        _logger = logger;
        _dbContextFactory = dbContextFactory;
        _fileHelper = fileHelper;
    }

    public async Task ExecuteAsync()
    {
        var processInfo = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var libraryInfo = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var provider = _sp.GetService<ISemanticKernelVectorStoreProvider>();
        var ingestion = _sp.GetService<IDocumentIngestionService>();

        if (provider == null || ingestion == null)
        {
            _logger.LogWarning("VectorStoreIdFixGrain: Required services not available (provider={HasProvider}, ingestion={HasIngestion}). Skipping.", provider != null, ingestion != null);
            return;
        }

        try
        {
            // Processes
            var processes = await processInfo.GetCombinedDocumentProcessInfoListAsync();
            var skProcesses = processes.Where(p => p.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore).ToList();
            foreach (var proc in skProcesses)
            {
                var indexName = proc.Repositories?.FirstOrDefault() ?? proc.ShortName;
                await FixForScopeAsync(indexName, proc.ShortName, DocumentLibraryType.PrimaryDocumentProcessLibrary, provider, ingestion);
            }

            // Libraries
            var libraries = await libraryInfo.GetAllDocumentLibrariesAsync();
            var skLibraries = libraries.Where(l => l.LogicType == DocumentProcessLogicType.SemanticKernelVectorStore).ToList();
            foreach (var lib in skLibraries)
            {
                await FixForScopeAsync(lib.IndexName, lib.ShortName, DocumentLibraryType.AdditionalDocumentLibrary, provider, ingestion);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "VectorStoreIdFixGrain: Error while executing fix job");
        }
    }

    private async Task FixForScopeAsync(
        string indexName,
        string shortName,
        DocumentLibraryType type,
        ISemanticKernelVectorStoreProvider provider,
        IDocumentIngestionService ingestion)
    {
        _logger.LogInformation("VectorStoreIdFixGrain: Scanning {Type} '{Name}' (index={Index}) for old IDs", type, shortName, indexName);

        const int pageSize = 250;
        int page = 0;
        int fixedCount = 0;
        int migratedCount = 0;
        int skippedCount = 0;

        while (true)
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var batch = await db.IngestedDocuments
                .Where(d => d.DocumentLibraryOrProcessName == shortName
                            && d.DocumentLibraryType == type
                            && d.IngestionState == IngestionState.Complete)
                .OrderBy(d => d.Id)
                .Skip(page * pageSize)
                .Take(pageSize)
                .ToListAsync();

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var doc in batch)
            {
                try
                {
                    var sanitizedFile = SanitizeFileName(doc.FileName);
                    var canonicalId = Base64UrlEncode(sanitizedFile);

                    if (string.Equals(doc.VectorStoreDocumentId, canonicalId, StringComparison.Ordinal))
                    {
                        skippedCount++;
                        continue;
                    }

                    var canonicalParts = await provider.GetDocumentPartitionNumbersAsync(indexName, canonicalId);
                    if (canonicalParts != null && canonicalParts.Count > 0)
                    {
                        await using var dbUpdate = await _dbContextFactory.CreateDbContextAsync();
                        var tracked = await dbUpdate.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == doc.Id);
                        if (tracked != null)
                        {
                            tracked.VectorStoreDocumentId = canonicalId;
                            tracked.VectorStoreIndexName = indexName;
                            tracked.IsVectorStoreIndexed = true;
                            tracked.VectorStoreIndexedDate ??= DateTime.UtcNow;
                            await dbUpdate.SaveChangesAsync();
                            fixedCount++;
                        }
                        continue;
                    }

                    var candidates = new HashSet<string>(StringComparer.Ordinal);
                    if (!string.IsNullOrWhiteSpace(doc.VectorStoreDocumentId)) candidates.Add(doc.VectorStoreDocumentId);
                    candidates.Add(sanitizedFile);
                    candidates.Remove(canonicalId);

                    bool foundOld = false;
                    foreach (var oldId in candidates)
                    {
                        var oldParts = await provider.GetDocumentPartitionNumbersAsync(indexName, oldId);
                        if (oldParts != null && oldParts.Count > 0)
                        {
                            foundOld = true;
                            break;
                        }
                    }

                    if (foundOld)
                    {
                        try
                        {
                            await ingestion.DeleteDocumentAsync(doc.DocumentLibraryOrProcessName!, indexName, sanitizedFile);
                        }
                        catch (Exception exDel)
                        {
                            _logger.LogWarning(exDel, "VectorStoreIdFixGrain: Delete before migrate failed for doc {DocId} ({File}) in {Name}", doc.Id, sanitizedFile, shortName);
                        }

                        var url = doc.FinalBlobUrl ?? doc.OriginalDocumentUrl;
                        await using var stream = await _fileHelper.GetFileAsStreamFromFullBlobUrlAsync(url);
                        if (stream == null)
                        {
                            _logger.LogWarning("VectorStoreIdFixGrain: Could not open stream for doc {DocId} at {Url}", doc.Id, url);
                            continue;
                        }

                        var result = await ingestion.IngestDocumentAsync(
                            doc.Id,
                            stream,
                            doc.FileName,
                            url,
                            doc.DocumentLibraryOrProcessName!,
                            indexName);

                        if (result.Success)
                        {
                            await using var dbUpdate2 = await _dbContextFactory.CreateDbContextAsync();
                            var tracked2 = await dbUpdate2.IngestedDocuments.FirstOrDefaultAsync(d => d.Id == doc.Id);
                            if (tracked2 != null)
                            {
                                tracked2.VectorStoreDocumentId = canonicalId;
                                tracked2.VectorStoreIndexName = indexName;
                                tracked2.IsVectorStoreIndexed = true;
                                tracked2.VectorStoreIndexedDate = DateTime.UtcNow;
                                tracked2.VectorStoreChunkCount = result.ChunkCount;
                                await dbUpdate2.SaveChangesAsync();
                                migratedCount++;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("VectorStoreIdFixGrain: Re-ingest failed for doc {DocId} ({File}) in {Name}: {Error}", doc.Id, sanitizedFile, shortName, result.ErrorMessage);
                        }
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
                catch (Exception exDoc)
                {
                    _logger.LogWarning(exDoc, "VectorStoreIdFixGrain: Error fixing doc {DocId} in {Name}", doc.Id, shortName);
                }
            }

            page++;
        }

        _logger.LogInformation("VectorStoreIdFixGrain: Completed for {Type} '{Name}' (index={Index}). Fixed={Fixed}, Migrated={Migrated}, Skipped={Skipped}",
            type, shortName, indexName, fixedCount, migratedCount, skippedCount);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return $"unknown_file_{Guid.NewGuid():N}";
        }

        return fileName
            .Replace(" ", "_")
            .Replace("+", "_")
            .Replace("~", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("*", "_")
            .Replace("?", "_")
            .Replace("\"", "_")
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace("|", "_");
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
