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
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Grains.Ingestion.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages.Reindexing.Events;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

[Reentrant]
public class RepositoryIndexMaintenanceGrain : Grain, IRepositoryIndexMaintenanceGrain
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<RepositoryIndexMaintenanceGrain> _logger;
    private readonly SearchIndexClient? _searchIndexClient;
    private readonly AzureFileHelper _fileHelper;
    private readonly IOptionsSnapshot<ServiceConfigurationOptions> _optionsSnapshot;
    private readonly NpgsqlDataSource? _npgsqlDataSource; // Now optional
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory; // Added for cleanup task
    private const string DummyDocumentContainer = "admin";
    private const string DummyDocumentName = "DummyDocument.pdf";
    
    // Cache for tracking reindexing operations
    private readonly Dictionary<string, string> _activeReindexingOperations = new();

    public RepositoryIndexMaintenanceGrain(
        IServiceProvider sp,
        ILogger<RepositoryIndexMaintenanceGrain> logger,
        AzureFileHelper fileHelper,
        IOptionsSnapshot<ServiceConfigurationOptions> optionsSnapshot,
        SearchIndexClient? searchIndexClient = null,
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
            if (_searchIndexClient == null)
            {
                _logger.LogWarning("AI Search is not enabled but UsePostgresMemory is false. No indexes will be discovered.");
                indexNamesList = [];
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
        }

        await CreateKernelMemoryIndexes(
            documentProcessInfoService, 
            documentLibraryInfoService,
            documentLibraryRepository, 
            indexNamesList);

        // Cleanup orphaned ingested document records for removed processes/libraries
        await RemoveOrphanedIngestedDocumentsAsync(documentProcessInfoService, documentLibraryInfoService);

        // Automatically validate schemas and trigger reindexing if needed
        try
        {
            await ValidateAndReindexSchemasAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate schemas and trigger reindexing during maintenance");
            // Don't fail the entire maintenance process due to schema validation issues
        }
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

        await dummyDocumentStream.DisposeAsync();
    }

    /// <inheritdoc/>
    public async Task ValidateAndReindexSchemasAsync()
    {
        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        
        // Try to get the SignalR notifier grain, but don't fail if it's not available (e.g., during silo startup)
        ISignalRNotifierGrain? signalRNotifier = null;
        try
        {
            signalRNotifier = _sp.GetService<ISignalRNotifierGrain>();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "SignalR notifier grain not available during schema validation - notifications will be skipped");
        }

        _logger.LogInformation("Starting schema validation and reindexing check for all vector stores");

        // Get all document processes and libraries
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();
        
        var vectorStoreProvider = _sp.GetRequiredService<ISemanticKernelVectorStoreProvider>();
        var embeddingService = _sp.GetRequiredService<IAiEmbeddingService>();

        // Check document processes
        foreach (var documentProcess in documentProcesses.Where(dp => dp.LogicType == DocumentProcessLogicType.KernelMemory))
        {
            // Get the correct embedding dimensions for this specific document process
            var (_, expectedDimensions) = await embeddingService.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcess.ShortName);
            
            foreach (var repository in documentProcess.Repositories)
            {
                await ValidateAndReindexIfNeeded(
                    vectorStoreProvider, signalRNotifier,
                    repository, documentProcess.ShortName, 
                    DocumentLibraryType.PrimaryDocumentProcessLibrary,
                    expectedDimensions);
            }
        }

        // Check document libraries  
        foreach (var documentLibrary in documentLibraries.Where(dl => dl.LogicType == DocumentProcessLogicType.KernelMemory))
        {
            // Get the correct embedding dimensions for this specific document library
            var (_, expectedDimensions) = await embeddingService.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibrary.ShortName);
            
            await ValidateAndReindexIfNeeded(
                vectorStoreProvider, signalRNotifier,
                documentLibrary.IndexName, documentLibrary.ShortName,
                DocumentLibraryType.AdditionalDocumentLibrary,
                expectedDimensions);
        }

        _logger.LogInformation("Completed schema validation and reindexing check");
    }

    /// <inheritdoc/>
    public async Task<IndexStatusSummary> GetIndexStatusSummaryAsync()
    {
        var documentProcessInfoService = _sp.GetRequiredService<IDocumentProcessInfoService>();
        var documentLibraryInfoService = _sp.GetRequiredService<IDocumentLibraryInfoService>();
        var vectorStoreProvider = _sp.GetRequiredService<ISemanticKernelVectorStoreProvider>();
        var embeddingService = _sp.GetRequiredService<IAiEmbeddingService>();
        
        var indexes = new List<IndexStatus>();

        // Check document processes
        var documentProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
        foreach (var documentProcess in documentProcesses.Where(dp => dp.LogicType == DocumentProcessLogicType.KernelMemory))
        {
            // Get the correct embedding dimensions for this specific document process
            var (_, expectedDimensions) = await embeddingService.ResolveEmbeddingConfigForDocumentProcessAsync(documentProcess.ShortName);
            
            foreach (var repository in documentProcess.Repositories)
            {
                var status = await GetIndexStatus(
                    vectorStoreProvider, repository, documentProcess.ShortName, 
                    DocumentLibraryType.PrimaryDocumentProcessLibrary, expectedDimensions);
                indexes.Add(status);
            }
        }

        // Check document libraries
        var documentLibraries = await documentLibraryInfoService.GetAllDocumentLibrariesAsync();
        foreach (var documentLibrary in documentLibraries.Where(dl => dl.LogicType == DocumentProcessLogicType.KernelMemory))
        {
            // Get the correct embedding dimensions for this specific document library
            var (_, expectedDimensions) = await embeddingService.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibrary.ShortName);
            
            var status = await GetIndexStatus(
                vectorStoreProvider, documentLibrary.IndexName, documentLibrary.ShortName,
                DocumentLibraryType.AdditionalDocumentLibrary, expectedDimensions);
            indexes.Add(status);
        }

        return new IndexStatusSummary { Indexes = indexes };
    }

    private async Task ValidateAndReindexIfNeeded(
        ISemanticKernelVectorStoreProvider vectorStoreProvider,
        ISignalRNotifierGrain? signalRNotifier,
        string indexName,
        string documentLibraryOrProcessName,
        DocumentLibraryType libraryType,
        int expectedEmbeddingDimensions)
    {
        try
        {
            var status = await GetIndexStatus(vectorStoreProvider, indexName, documentLibraryOrProcessName, libraryType, expectedEmbeddingDimensions);
            
            if (status.Status == IndexHealthStatus.SchemaIncompatible)
            {
                if (signalRNotifier != null)
                {
                    _logger.LogWarning("Index {IndexName} for {LibraryOrProcess} has incompatible schema. Triggering reindexing.", 
                        indexName, documentLibraryOrProcessName);

                    await TriggerReindexing(signalRNotifier, indexName, documentLibraryOrProcessName, libraryType, status.StatusMessage);
                }
                else
                {
                    _logger.LogWarning("Index {IndexName} for {LibraryOrProcess} has incompatible schema, but SignalR notifier is not available. Reindexing will be skipped during this maintenance cycle.", 
                        indexName, documentLibraryOrProcessName);
                }
            }
            else if (status.Status == IndexHealthStatus.Healthy)
            {
                _logger.LogDebug("Index {IndexName} for {LibraryOrProcess} is healthy", indexName, documentLibraryOrProcessName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate schema for index {IndexName}", indexName);
        }
    }

    private async Task<IndexStatus> GetIndexStatus(
        ISemanticKernelVectorStoreProvider vectorStoreProvider,
        string indexName,
        string documentLibraryOrProcessName,
        DocumentLibraryType libraryType,
        int expectedEmbeddingDimensions)
    {
        var status = new IndexStatus
        {
            IndexName = indexName,
            DocumentLibraryOrProcessName = documentLibraryOrProcessName,
            Status = IndexHealthStatus.Unknown,
            LastCheckedUtc = DateTime.UtcNow,
            EmbeddingDimensions = expectedEmbeddingDimensions,
            ReindexingRunId = _activeReindexingOperations.GetValueOrDefault(indexName)
        };

        try
        {
            // Check if reindexing is active
            if (_activeReindexingOperations.ContainsKey(indexName))
            {
                status.Status = IndexHealthStatus.Reindexing;
                status.StatusMessage = "Reindexing in progress";
                return status;
            }

            // Check if collection exists
            bool collectionExists = await vectorStoreProvider.CollectionExistsAsync(indexName);
            if (!collectionExists)
            {
                status.Status = IndexHealthStatus.Missing;
                status.StatusMessage = "Index does not exist";
                return status;
            }

            // Try to validate schema by creating a test collection with expected schema
            try
            {
                await vectorStoreProvider.EnsureCollectionAsync(indexName, expectedEmbeddingDimensions);
                
                // Test if we can query the collection to validate DocumentReference field exists
                var hasDocumentReference = await TestDocumentReferenceField(vectorStoreProvider, indexName);
                status.HasDocumentReferenceField = hasDocumentReference;
                
                if (!hasDocumentReference)
                {
                    status.Status = IndexHealthStatus.SchemaIncompatible;
                    status.StatusMessage = "Missing DocumentReference field (legacy schema detected)";
                    return status;
                }

                status.Status = IndexHealthStatus.Healthy;
                status.StatusMessage = "Schema is compatible";
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("dimension mismatch") || ex.Message.Contains("Vector dimension mismatch"))
            {
                status.Status = IndexHealthStatus.SchemaIncompatible;
                status.StatusMessage = $"Vector dimension mismatch: {ex.Message}";

                // Try to extract actual dimensions from error message
                var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"expects a length of (\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int actualDims))
                {
                    status.StatusMessage += $" (expected: {expectedEmbeddingDimensions}, actual: {actualDims})";
                }
            }
            catch (Microsoft.Extensions.VectorData.VectorStoreException vex) when (vex.InnerException is Npgsql.PostgresException pgEx && pgEx.Message.Contains("expected") && pgEx.Message.Contains("dimensions"))
            {
                // PostgreSQL dimension mismatch - auto-clear only for system indexes
                if (indexName.StartsWith("system-", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("PostgreSQL vector dimension mismatch detected for system index {IndexName}. Expected: {Expected} dimensions. Auto-clearing and recreating collection.", indexName, expectedEmbeddingDimensions);

                    try
                    {
                        // Clear the existing collection (system indexes only)
                        await vectorStoreProvider.ClearCollectionAsync(indexName);
                        _logger.LogInformation("Cleared incompatible system collection {IndexName}", indexName);

                        // Recreate with correct dimensions
                        await vectorStoreProvider.EnsureCollectionAsync(indexName, expectedEmbeddingDimensions);
                        _logger.LogInformation("Recreated system collection {IndexName} with {Dimensions} dimensions", indexName, expectedEmbeddingDimensions);

                        status.Status = IndexHealthStatus.Recreated;
                        status.StatusMessage = $"System collection recreated due to dimension mismatch (now uses {expectedEmbeddingDimensions} dimensions)";
                    }
                    catch (Exception recreateEx)
                    {
                        _logger.LogError(recreateEx, "Failed to recreate system collection {IndexName} after dimension mismatch", indexName);
                        status.Status = IndexHealthStatus.SchemaIncompatible;
                        status.StatusMessage = $"Dimension mismatch detected but failed to recreate: {recreateEx.Message}";
                    }
                }
                else
                {
                    // User index - require manual intervention
                    _logger.LogWarning("PostgreSQL vector dimension mismatch detected for user index {IndexName}. Expected: {Expected} dimensions. Manual intervention required - user indexes are not auto-cleared.", indexName, expectedEmbeddingDimensions);
                    status.Status = IndexHealthStatus.SchemaIncompatible;
                    status.StatusMessage = $"Vector dimension mismatch detected. Manual reindex required for user-created collection (expected: {expectedEmbeddingDimensions} dimensions)";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not validate schema for index {IndexName}, assuming healthy", indexName);
                status.Status = IndexHealthStatus.Healthy;
                status.StatusMessage = $"Validation failed but assuming healthy: {ex.Message}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for index {IndexName}", indexName);
            status.Status = IndexHealthStatus.Unknown;
            status.StatusMessage = $"Status check failed: {ex.Message}";
        }

        return status;
    }

    private async Task<bool> TestDocumentReferenceField(ISemanticKernelVectorStoreProvider vectorStoreProvider, string indexName)
    {
        try
        {
            // Simple approach: search for a minimal set of records and examine the TagsJson field
            // New schema will have "DocumentReference" in TagsJson, old schema will have "OriginalDocumentUrl"
            var testEmbedding = Enumerable.Repeat(0.1f, 1536).ToArray(); // Standard dimension test vector
            
            var results = await vectorStoreProvider.SearchAsync(indexName, testEmbedding, 5, 0.0); // Get up to 5 records
            
            // Examine the search results - VectorSearchMatch should contain the record data
            foreach (var result in results)
            {
                // Check if the result contains information about DocumentReference
                // The VectorSearchMatch should contain the record data that we can examine
                var record = result.Record;
                if (record != null)
                {
                    // Check if record has Tags that contain DocumentReference (new schema) vs OriginalDocumentUrl (old schema)
                    var tags = record.Tags;
                    if (tags != null && tags.ContainsKey("DocumentReference"))
                    {
                        _logger.LogDebug("Found DocumentReference in tags for index {IndexName}, new schema detected", indexName);
                        return true;
                    }
                    if (tags != null && tags.ContainsKey("OriginalDocumentUrl"))
                    {
                        _logger.LogDebug("Found OriginalDocumentUrl in tags for index {IndexName}, legacy schema detected", indexName);
                        return false;
                    }
                }
            }
            
            // If we can't find any records or determine from tags, assume new schema for safety
            _logger.LogDebug("Could not determine schema from records in index {IndexName}, assuming new schema", indexName);
            return true;
        }
        catch (Exception ex)
        {
            // Schema issues or other problems - check the error message for clues
            var message = ex.Message.ToLowerInvariant();
            if (message.Contains("originaldocumenturl") || 
                message.Contains("legacy") ||
                message.Contains("old schema"))
            {
                _logger.LogDebug(ex, "Error suggests legacy schema for index {IndexName}", indexName);
                return false;
            }
            
            if (message.Contains("documentreference") || 
                message.Contains("unknown field") || 
                message.Contains("schema") ||
                message.Contains("property") ||
                message.Contains("field"))
            {
                _logger.LogDebug(ex, "Error suggests schema issues for index {IndexName}, could be either schema", indexName);
                // If schema issues, we're not sure - let's assume legacy and trigger reindex
                return false;
            }
            
            // Other errors might not be schema-related, so we assume new schema for safety
            _logger.LogDebug(ex, "Unexpected error testing schema for index {IndexName}, assuming new schema", indexName);
            return true;
        }
    }

    private async Task TriggerReindexing(
        ISignalRNotifierGrain? signalRNotifier,
        string indexName,
        string documentLibraryOrProcessName,
        DocumentLibraryType libraryType,
        string? reason)
    {
        try
        {
            var runId = Guid.NewGuid().ToString();
            _activeReindexingOperations[indexName] = runId;

            _logger.LogInformation("Starting reindexing for {IndexName} ({LibraryOrProcess}) due to: {Reason}", 
                indexName, documentLibraryOrProcessName, reason);

            // Send notification about reindexing start (if SignalR notifier is available)
            if (signalRNotifier != null)
            {
                var startNotification = new DocumentReindexStartedNotification(
                    runId, // OrchestrationId
                    documentLibraryOrProcessName, // DocumentLibraryOrProcessName  
                    reason ?? "Schema incompatibility detected" // Reason
                );

                await signalRNotifier.NotifyDocumentReindexStartedAsync(startNotification);
            }
            else
            {
                _logger.LogDebug("SignalR notifier not available - skipping reindex start notification for {IndexName}", indexName);
            }

            // Get the appropriate grain to trigger reindexing
            var reindexGrain = GrainFactory.GetGrain<IDocumentReindexOrchestrationGrain>(runId);
            
            if (libraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
            {
                await reindexGrain.StartDocumentProcessReindexingAsync(documentLibraryOrProcessName, reason ?? "Schema compatibility issue detected");
            }
            else if (libraryType == DocumentLibraryType.AdditionalDocumentLibrary)
            {
                await reindexGrain.StartDocumentLibraryReindexingAsync(documentLibraryOrProcessName, reason ?? "Schema compatibility issue detected");
            }

            _logger.LogInformation("Reindexing request submitted for {IndexName} with run ID {RunId}", indexName, runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger reindexing for index {IndexName}", indexName);
            
            // Remove from active operations if failed to start  
            _activeReindexingOperations.Remove(indexName);
            
            // Send failure notification (if SignalR notifier is available)
            if (signalRNotifier != null)
            {
                var failureNotification = new DocumentReindexFailedNotification(
                    _activeReindexingOperations.GetValueOrDefault(indexName, "unknown"), // OrchestrationId
                    documentLibraryOrProcessName, // DocumentLibraryOrProcessName
                    ex.Message // ErrorMessage
                );

                await signalRNotifier.NotifyDocumentReindexFailedAsync(failureNotification);
            }
            else
            {
                _logger.LogDebug("SignalR notifier not available - skipping reindex failure notification for {IndexName}", indexName);
            }
        }
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
