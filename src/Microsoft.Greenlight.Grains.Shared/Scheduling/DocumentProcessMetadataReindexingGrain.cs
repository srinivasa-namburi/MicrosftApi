// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Orleans.Concurrency;
using System.Text;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling
{
    /// <summary>
    /// Scheduled grain that reindexes document process metadata for Flow intent detection.
    /// Runs every 30 minutes to keep the system index up to date with document process changes.
    /// </summary>
    [Reentrant]
    public class DocumentProcessMetadataReindexingGrain : Grain, IDocumentProcessMetadataReindexingGrain
    {
        private readonly ILogger<DocumentProcessMetadataReindexingGrain> _logger;
        private readonly IServiceProvider _sp;

        public DocumentProcessMetadataReindexingGrain(
            ILogger<DocumentProcessMetadataReindexingGrain> logger,
            IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Document process metadata reindexing job started at {time}", DateTimeOffset.Now);

            try
            {
                // Necessary to use scoped services
                using var scope = _sp.CreateScope();
                var documentProcessInfoService = scope.ServiceProvider.GetRequiredService<IDocumentProcessInfoService>();
                var documentRepositoryFactory = scope.ServiceProvider.GetRequiredService<IDocumentRepositoryFactory>();

                // Get all available document processes
                var availableProcesses = await documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();

                if (!availableProcesses.Any())
                {
                    _logger.LogInformation("No document processes found for metadata indexing");
                    return;
                }

                _logger.LogInformation("Found {ProcessCount} document processes for metadata reindexing", availableProcesses.Count);

                // Create a synthetic DocumentProcessInfo with custom chunking settings for metadata indexing
                // Small chunks (150 tokens) with no overlap work best for metadata intent detection
                var syntheticDocumentProcess = new Microsoft.Greenlight.Shared.Contracts.DTO.DocumentProcessInfo
                {
                    Id = Guid.NewGuid(), // Synthetic GUID for this system process
                    ShortName = SystemIndexes.DocumentProcessMetadataIntentIndex,
                    Description = "System index for document process metadata intent detection",
                    BlobStorageContainerName = "system-metadata", // Required property
                    LogicType = DocumentProcessLogicType.SemanticKernelVectorStore,
                    VectorStoreChunkSize = 150, // Small chunks for precise metadata matching
                    VectorStoreChunkOverlap = 0, // No overlap needed for metadata
                    VectorStoreChunkingMode = Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Simple,
                    Repositories = new List<string>()
                };

                // Create repository using the synthetic document process to get custom chunking behavior
                var intentRepository = await documentRepositoryFactory.CreateForDocumentProcessAsync(syntheticDocumentProcess);

                // Reindex all document processes in parallel
                var indexingTasks = availableProcesses.Select(process => IndexDocumentProcessMetadataAsync(intentRepository, process));
                await Task.WhenAll(indexingTasks);

                _logger.LogInformation("Document process metadata reindexing completed successfully. Indexed {ProcessCount} document processes.", availableProcesses.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during document process metadata reindexing");
                // Non-fatal - will retry on next scheduled run
            }
        }

        /// <summary>
        /// Indexes a single document process's metadata for intent detection.
        /// </summary>
        private async Task IndexDocumentProcessMetadataAsync(IDocumentRepository intentRepository, Microsoft.Greenlight.Shared.Contracts.DTO.DocumentProcessInfo process)
        {
            try
            {
                // Build comprehensive metadata text for vector embedding
                var metadataBuilder = new StringBuilder();
                metadataBuilder.AppendLine($"Document Process: {process.ShortName}");

                if (!string.IsNullOrEmpty(process.Description))
                {
                    metadataBuilder.AppendLine($"Description: {process.Description}");
                }

                if (!string.IsNullOrEmpty(process.OutlineText))
                {
                    metadataBuilder.AppendLine($"Content Outline: {process.OutlineText}");
                }

                // Add process configuration context
                metadataBuilder.AppendLine($"Process Type: {process.LogicType}");
                metadataBuilder.AppendLine($"Citations: {process.NumberOfCitationsToGetFromRepository}");

                if (process.Repositories?.Any() == true)
                {
                    metadataBuilder.AppendLine($"Associated Repositories: {string.Join(", ", process.Repositories)}");
                }

                var metadataText = metadataBuilder.ToString();
                var metadataBytes = System.Text.Encoding.UTF8.GetBytes(metadataText);

                // Store the metadata using the standard vector store interface
                var fileName = $"process-{process.ShortName}.metadata";
                var additionalTags = new Dictionary<string, string>
                {
                    ["documentProcessId"] = process.Id.ToString(),
                    ["documentProcessName"] = process.ShortName,
                    ["processType"] = process.LogicType.ToString(),
                    ["metadataType"] = "intent-detection",
                    ["systemIndex"] = "true",
                    ["lastReindexed"] = DateTime.UtcNow.ToString("O") // ISO 8601 format
                };

                using var metadataStream = new MemoryStream(metadataBytes);
                await intentRepository.StoreContentAsync(
                    documentLibraryName: SystemIndexes.DocumentProcessMetadataIntentIndex,
                    indexName: SystemIndexes.DocumentProcessMetadataIntentIndex,
                    fileStream: metadataStream,
                    fileName: fileName,
                    documentReference: process.Id.ToString(),
                    userId: "system-scheduled", // System-level scheduled indexing
                    additionalTags: additionalTags
                );

                _logger.LogDebug("Reindexed document process {ProcessName} for intent detection", process.ShortName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error reindexing document process {ProcessName} for intent detection", process.ShortName);
                // Non-fatal - continue with other processes
            }
        }
    }
}