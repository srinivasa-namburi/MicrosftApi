// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Npgsql;
using Orleans.Concurrency;
using Azure.Search.Documents.Indexes;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class RepositoryIndexMaintenanceGrain : Grain, IRepositoryIndexMaintenanceGrain
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RepositoryIndexMaintenanceGrain> _logger;
    private readonly SearchIndexClient _searchIndexClient;
    private readonly AzureFileHelper _fileHelper;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private readonly NpgsqlDataSource? _npgsqlDataSource; // Now optional
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory; // Added for cleanup task
    private const string DummyDocumentContainer = "admin";
    private const string DummyDocumentName = "DummyDocument.pdf";

    public RepositoryIndexMaintenanceGrain(
        IServiceProvider sp,
        ILogger<RepositoryIndexMaintenanceGrain> logger,
        SearchIndexClient searchIndexClient,
        AzureFileHelper fileHelper,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        NpgsqlDataSource? npgsqlDataSource = null, // Now optional
        IDbContextFactory<DocGenerationDbContext>? dbContextFactory = null)
    {
        _optionsSnapshot = optionsSnapshot;
        _sp = sp;
        _logger = logger;
        _searchIndexClient = searchIndexClient;
        _fileHelper = fileHelper;
        _npgsqlDataSource = npgsqlDataSource; // Now optional
        _dbContextFactory = dbContextFactory ?? _sp.GetRequiredService<IDbContextFactory<DocGenerationDbContext>>();
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync()
    {
        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var documentLibraryRepository = _sp.GetRequiredService<IAdditionalDocumentLibraryKernelMemoryRepository>();

        List<string> indexNamesList;

        if (_optionsSnapshot.Value.GreenlightServices.Global.UsePostgresMemory)
        {
            if (_npgsqlDataSource == null)
            {
                _logger.LogWarning("UsePostgresMemory is enabled but NpgsqlDataSource is not configured. Skipping Postgres index discovery.");
                indexNamesList = [];
            }
            else
            {
                // Query Postgres for tables in the km schema with names starting with 'km-'
                var indexNames = new HashSet<string>();
                await using var conn = await _npgsqlDataSource.OpenConnectionAsync();
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT tablename FROM pg_tables WHERE schemaname = 'km' AND tablename LIKE 'km-%'";
                    await using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        var tableName = reader.GetString(0);
                        if (tableName.StartsWith("km-"))
                        {
                            indexNames.Add(tableName.Substring(3)); // Remove 'km-' prefix
                        }
                    }
                }
                indexNamesList = indexNames.ToList();
            }
        }
        else
        {
            var indexNames = new HashSet<string>();
            await foreach (var indexName in _searchIndexClient.GetIndexNamesAsync(CancellationToken.None))
            {
                indexNames.Add(indexName);
            }
            indexNamesList = indexNames.ToList();
        }

        await CreateKernelMemoryIndexes(
            documentProcessInfoService, 
            documentLibraryInfoService,
            documentLibraryRepository, 
            indexNamesList);

        // Cleanup orphaned ingested document records for removed processes/libraries
        await RemoveOrphanedIngestedDocumentsAsync(documentProcessInfoService, documentLibraryInfoService);
    }

    private async Task RemoveOrphanedIngestedDocumentsAsync(
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService)
    {
        try
        {
            var activeProcesses = (await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync())
                .Select(p => p.ShortName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeLibraries = (await documentLibraryInfoService.GetAllDocumentLibrariesAsync())
                .Select(l => l.ShortName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await using var db = await _dbContextFactory.CreateDbContextAsync();

            // Delete AdditionalDocumentLibrary ingested docs where the library no longer exists
            var deletedLibCount = await db.IngestedDocuments
                .Where(x => x.DocumentLibraryType == DocumentLibraryType.AdditionalDocumentLibrary
                            && x.DocumentLibraryOrProcessName != null
                            && !activeLibraries.Contains(x.DocumentLibraryOrProcessName))
                .ExecuteDeleteAsync();

            if (deletedLibCount > 0)
            {
                _logger.LogInformation("RepositoryIndexMaintenance: Deleted {Count} orphaned ingested documents for removed libraries.", deletedLibCount);
            }

            // Delete PrimaryDocumentProcessLibrary ingested docs where the process no longer exists
            var deletedProcCount = await db.IngestedDocuments
                .Where(x => x.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary
                            && x.DocumentLibraryOrProcessName != null
                            && !activeProcesses.Contains(x.DocumentLibraryOrProcessName))
                .ExecuteDeleteAsync();

            if (deletedProcCount > 0)
            {
                _logger.LogInformation("RepositoryIndexMaintenance: Deleted {Count} orphaned ingested documents for removed document processes.", deletedProcCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RepositoryIndexMaintenance: Failed to remove orphaned ingested documents.");
        }
    }

    private async Task CreateKernelMemoryIndexes(
        IDocumentProcessInfoService documentProcessInfoService,
        IDocumentLibraryInfoService documentLibraryInfoService,
        IAdditionalDocumentLibraryKernelMemoryRepository additionalDocumentLibraryKernelMemoryRepository,
        IReadOnlyList<string> indexNames)
    {
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var kernelMemoryDocumentProcesses = documentProcesses.Where(x => x.LogicType == DocumentProcessLogicType.KernelMemory).ToList();

        if (kernelMemoryDocumentProcesses.Count == 0)
        {
            _logger.LogDebug("No Kernel Memory-based Document Processes found. Skipping index creation.");
            return;
        }

        _logger.LogDebug("Creating or updating Kernel Memory indexes for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        // Get dummy document stream from blob storage or upload it if it doesn't exist
        Stream? dummyDocumentStream = await GetOrCreateDummyDocumentAsync();
        if (dummyDocumentStream == null)
        {
            _logger.LogError("Failed to get dummy document. Cannot create indexes.");
            return;
        }

        foreach (var documentProcess in kernelMemoryDocumentProcesses)
        {
            // Resolve the kernel memory repository from DI with a keyed service if configured
            IKernelMemoryRepository? kernelMemoryRepository = null;
            try
            {
                using var scope = _sp.CreateScope();
                kernelMemoryRepository = scope.ServiceProvider.GetKeyedService<IKernelMemoryRepository>($"{documentProcess.ShortName}-IKernelMemory");
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to resolve keyed KM repository for {Process}", documentProcess.ShortName);
            }

            if (kernelMemoryRepository == null)
            {
                _logger.LogError("No Kernel Memory repository registered for Document Process {DocumentProcess} - skipping", documentProcess.ShortName);
                continue;
            }

            foreach (var repository in documentProcess.Repositories)
            {
                if (indexNames.Contains(repository))
                {
                    _logger.LogDebug("Index {IndexName} already exists for Document Process {DocumentProcess}. Skipping creation.", repository, documentProcess.ShortName);
                    continue;
                }

                var currentTimeUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var dummyDocumentCreatedFileName = $"DummyDocument-{currentTimeUnixTime}.pdf";
                _logger.LogInformation("Creating index for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                
                // Reset stream position
                dummyDocumentStream.Position = 0;
                
                await kernelMemoryRepository.StoreContentAsync(documentProcess.ShortName, repository, dummyDocumentStream, dummyDocumentCreatedFileName, null);
                
                _logger.LogInformation("Index created for Document Process {DocumentProcess} and Repository {Repository}", documentProcess.ShortName, repository);
                await kernelMemoryRepository.DeleteContentAsync(documentProcess.ShortName, repository, dummyDocumentCreatedFileName);
            }
        }

        _logger.LogDebug("Kernel Memory indexes created for {Count} Document Processes", kernelMemoryDocumentProcesses.Count);

        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();

        if (documentLibraries.Count == 0)
        {
            _logger.LogDebug("No Document Libraries found. Skipping index creation.");
            dummyDocumentStream.Dispose();
            return;
        }

        // Filter to Kernel Memory libraries only; skip Semantic Kernel Vector Store libraries (created lazily on demand)
        var kernelMemoryLibraries = documentLibraries
            .Where(l => l.LogicType == DocumentProcessLogicType.KernelMemory)
            .ToList();

        var skippedLibraries = documentLibraries.Count - kernelMemoryLibraries.Count;
        if (skippedLibraries > 0)
        {
            _logger.LogDebug("Skipping {Skipped} non-KernelMemory libraries for proactive index creation (handled on-demand by their providers)", skippedLibraries);
        }

        if (kernelMemoryLibraries.Count == 0)
        {
            _logger.LogDebug("No Kernel Memory-based Document Libraries found. Skipping library index creation phase.");
            dummyDocumentStream.Dispose();
            return;
        }

        if (kernelMemoryLibraries.Count > 0)
        {
            _logger.LogDebug("Creating indexes for {Count} Kernel Memory Document Libraries", kernelMemoryLibraries.Count);
        }

        foreach (var documentLibrary in kernelMemoryLibraries)
        {
            if (indexNames.Contains(documentLibrary.IndexName))
            {
                _logger.LogDebug("Index {IndexName} already exists for Document Library {DocumentLibrary}. Skipping creation.", documentLibrary.IndexName, documentLibrary.ShortName);
                continue;
            }

            _logger.LogInformation("Creating index for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            
            // Reset stream position
            dummyDocumentStream.Position = 0;
            
            await additionalDocumentLibraryKernelMemoryRepository.StoreContentAsync(
                documentLibrary.ShortName, 
                documentLibrary.IndexName, 
                dummyDocumentStream, 
                "DummyDocument.pdf", 
                null);
            
            _logger.LogInformation("Index created for Document Library {DocumentLibrary}", documentLibrary.ShortName);
            await additionalDocumentLibraryKernelMemoryRepository.DeleteContentAsync(
                documentLibrary.ShortName, 
                documentLibrary.IndexName, 
                "DummyDocument.pdf");
        }

        dummyDocumentStream.Dispose();
    }

    private async Task<Stream?> GetOrCreateDummyDocumentAsync()
    {
        try
        {
            // First try to get the document from blob storage
            try
            {
                var stream = await _fileHelper.GetFileAsStreamFromContainerAndBlobName(
                    DummyDocumentContainer, 
                    DummyDocumentName);
                
                if (stream != null)
                {
                    _logger.LogDebug("Found DummyDocument.pdf in admin container");
                    return stream;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not retrieve DummyDocument.pdf from blob storage. Will attempt to create it.");
            }

            // If we get here, we need to create and upload a new dummy document
            // Create a simple PDF with minimal content
            using (var memoryStream = new MemoryStream())
            {
                await using (var writer = new StreamWriter(memoryStream, leaveOpen: true))
                {
                    await writer.WriteLineAsync("%PDF-1.4");
                    await writer.WriteLineAsync("1 0 obj");
                    await writer.WriteLineAsync("<< /Type /Catalog /Pages 2 0 R >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("2 0 obj");
                    await writer.WriteLineAsync("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("3 0 obj");
                    await writer.WriteLineAsync("<< /Type /Page /Parent 2 0 R /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("4 0 obj");
                    await writer.WriteLineAsync("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("5 0 obj");
                    await writer.WriteLineAsync("<< /Length 44 >>");
                    await writer.WriteLineAsync("stream");
                    await writer.WriteLineAsync("BT /F1 12 Tf 72 712 Td (Dummy Document) Tj ET");
                    await writer.WriteLineAsync("endstream");
                    await writer.WriteLineAsync("endobj");
                    await writer.WriteLineAsync("xref");
                    await writer.WriteLineAsync("0 6");
                    await writer.WriteLineAsync("0000000000 65535 f");
                    await writer.WriteLineAsync("0000000009 00000 n");
                    await writer.WriteLineAsync("0000000063 00000 n");
                    await writer.WriteLineAsync("0000000122 00000 n");
                    await writer.WriteLineAsync("0000000228 00000 n");
                    await writer.WriteLineAsync("0000000296 00000 n");
                    await writer.WriteLineAsync("trailer");
                    await writer.WriteLineAsync("<< /Size 6 /Root 1 0 R >>");
                    await writer.WriteLineAsync("startxref");
                    await writer.WriteLineAsync("385");
                    await writer.WriteLineAsync("%%EOF");
                }

                // Reset the position to the beginning
                memoryStream.Position = 0;

                // Upload to blob storage
                try
                {
                    _logger.LogDebug("Uploading new DummyDocument.pdf to admin container");
                    await _fileHelper.UploadFileToBlobAsync(
                        memoryStream, 
                        DummyDocumentName, 
                        DummyDocumentContainer, 
                        true);
                    
                    // Get a fresh stream for the newly uploaded document
                    var uploadedStream = await _fileHelper.GetFileAsStreamFromContainerAndBlobName(
                        DummyDocumentContainer, 
                        DummyDocumentName);
                    
                    if (uploadedStream != null)
                    {
                        _logger.LogDebug("Successfully created and uploaded DummyDocument.pdf");
                        return uploadedStream;
                    }
                }
                catch (Exception ex)
                {
                    // Return the in-memory stream as a fallback
                    memoryStream.Position = 0;
                    
                    // Create a new memory stream with the content to avoid disposal issues
                    var fallbackStream = new MemoryStream();
                    await memoryStream.CopyToAsync(fallbackStream);
                    fallbackStream.Position = 0;
                    
                    _logger.LogWarning("Using in-memory fallback for DummyDocument.pdf");
                    return fallbackStream;
                }
            }

            _logger.LogError("Failed to create or retrieve DummyDocument.pdf");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrCreateDummyDocumentAsync");
            return null;
        }
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
