// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using System.Text;

namespace Microsoft.Greenlight.Shared.Services.ContentReference
{
    /// <summary>
    /// Service for building RAG contexts from content references
    /// </summary>
    public class RagContextBuilder : IRagContextBuilder
    {
        private readonly IContentReferenceService _contentReferenceService;
        private readonly IAiEmbeddingService _aiEmbeddingService;
        private readonly ISemanticKernelVectorStoreProvider _vectorStoreProvider;
        private readonly IOptionsMonitor<ServiceConfigurationOptions> _options;
        private readonly ILogger<RagContextBuilder> _logger;

        public RagContextBuilder(
            IContentReferenceService contentReferenceService,
            IAiEmbeddingService aiEmbeddingService,
            ISemanticKernelVectorStoreProvider vectorStoreProvider,
            IOptionsMonitor<ServiceConfigurationOptions> options,
            ILogger<RagContextBuilder> logger)
        {
            _contentReferenceService = contentReferenceService;
            _aiEmbeddingService = aiEmbeddingService;
            _vectorStoreProvider = vectorStoreProvider;
            _options = options;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> BuildContextWithSelectedReferencesAsync(
            string userQuery,
            List<ContentReferenceItem> allReferences,
            int topN = 5,
            int maxChunkTokens = 1200)
        {
            var contextStringBuilder = new StringBuilder();
            contextStringBuilder.AppendLine("[Context]");
            contextStringBuilder.AppendLine("The following are pre-rendered chunks of parts of the document(s) picked to answer the user's question:");

            if (allReferences.Any())
            {
                try
                {
                    // Check if we have a content editing reference (first reference with specific name)
                    var contentBeingEdited = allReferences.FirstOrDefault(r =>
                        r.DisplayName?.Contains("Content Being Edited") == true ||
                        r.ReferenceType == ContentReferenceType.GeneratedSection);

                    if (contentBeingEdited != null && !string.IsNullOrEmpty(contentBeingEdited.RagText))
                    {
                        // Always include the content being edited at the top
                        contextStringBuilder.AppendLine("[CONTENT BEING EDITED]");
                        contextStringBuilder.AppendLine(contentBeingEdited.RagText);
                        contextStringBuilder.AppendLine("[/CONTENT BEING EDITED]");
                        contextStringBuilder.AppendLine();

                        // Remove transient references from the list we'll process for embeddings
                        allReferences = allReferences.Where(r => r.Id != contentBeingEdited.Id).ToList();

                        // Decrease topN by 1 since we're including the edited content separately
                        if (topN > 1) topN--;
                    }

                    // Only process references with valid IDs for vector search
                    var referencesForSearch = allReferences
                        .Where(r => r.Id != Guid.Empty && r.Id != default)
                        .ToList();

                    if (referencesForSearch.Any())
                    {
                        try
                        {
                            var vsOpts = _options.CurrentValue.GreenlightServices.VectorStore;
                            var minRelevance = vsOpts.MinRelevanceScore <= 0 ? 0.7 : vsOpts.MinRelevanceScore;
                            var maxSearchResults = vsOpts.MaxSearchResults > 0 ? vsOpts.MaxSearchResults : topN;

                            var globalMatches = new List<(string IndexName, SkVectorChunkRecord Record, double Score)>();

                            // Group by content reference type to use per-type index + embedding config
                            foreach (var grp in referencesForSearch.GroupBy(r => r.ReferenceType))
                            {
                                var type = grp.Key;
                                var indexName = GetIndexName(type);

                                // Resolve embedding config for the type
                                var (deployment, dims) = await _aiEmbeddingService.ResolveEmbeddingConfigForContentReferenceTypeAsync(type);
                                var queryEmbedding = await _aiEmbeddingService.GenerateEmbeddingsAsync(userQuery, deployment, dims);

                                foreach (var reference in grp)
                                {
                                    try
                                    {
                                        // Filter to chunks for this reference only
                                        var filters = new Dictionary<string, string>
                                        {
                                            ["contentReferenceId"] = reference.Id.ToString()
                                        };

                                        var results = await _vectorStoreProvider.SearchAsync(
                                            indexName,
                                            queryEmbedding,
                                            top: Math.Min(maxSearchResults, Math.Max(1, topN)),
                                            minRelevance: minRelevance,
                                            parametersExactMatch: filters);

                                        if (results is { Count: > 0 })
                                        {
                                            foreach (var match in results)
                                            {
                                                if (!string.IsNullOrWhiteSpace(match.Record.ChunkText))
                                                {
                                                    globalMatches.Add((indexName, match.Record, match.Score));
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Vector search failed for reference {RefId} on index {Index}; continuing", reference.Id, indexName);
                                    }
                                }
                            }

                            if (globalMatches.Count > 0)
                            {
                                var ordered = globalMatches
                                    .OrderByDescending(x => x.Score)
                                    .ToList();

                                var baseHits = ordered.Take(topN).ToList();

                                // Deduplicate by (index, docId, partition) across base + neighbors
                                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                foreach (var hit in baseHits)
                                {
                                    var baseKey = $"{hit.IndexName}|{hit.Record.DocumentId}|{hit.Record.PartitionNumber}";
                                    if (seen.Add(baseKey))
                                    {
                                        contextStringBuilder.AppendLine(hit.Record.ChunkText);
                                        contextStringBuilder.AppendLine();
                                    }

                                    // Neighbor expansion: 1 preceding, 1 following
                                    try
                                    {
                                        var neighbors = await _vectorStoreProvider.GetNeighborChunksAsync(
                                            hit.IndexName,
                                            hit.Record.DocumentId,
                                            hit.Record.PartitionNumber,
                                            precedingPartitions: 1,
                                            followingPartitions: 1);

                                        foreach (var n in neighbors.OrderBy(n => n.PartitionNumber))
                                        {
                                            var nKey = $"{hit.IndexName}|{n.DocumentId}|{n.PartitionNumber}";
                                            if (seen.Add(nKey) && !string.IsNullOrWhiteSpace(n.ChunkText))
                                            {
                                                contextStringBuilder.AppendLine(n.ChunkText);
                                                contextStringBuilder.AppendLine();
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Neighbor expansion failed for {Doc}/{Part} in {Index}", hit.Record.DocumentId, hit.Record.PartitionNumber, hit.IndexName);
                                    }
                                }
                            }
                            else
                            {
                                AddFallbackChunks(contextStringBuilder, referencesForSearch);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error performing SK search for context references, using fallback approach");
                            AddFallbackChunks(contextStringBuilder, referencesForSearch);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error building context with selected references");

                    // Provide a simplified fallback approach that doesn't depend on embeddings
                    contextStringBuilder.AppendLine("Error processing references. Using direct content:");

                    // Just add the content being edited directly
                    var contentBeingEdited = allReferences.FirstOrDefault(r =>
                        r.DisplayName?.Contains("Content Being Edited") == true ||
                        r.ReferenceType == ContentReferenceType.GeneratedSection);

                    if (contentBeingEdited != null && !string.IsNullOrEmpty(contentBeingEdited.RagText))
                    {
                        contextStringBuilder.AppendLine(contentBeingEdited.RagText);
                    }
                }
            }
            else
            {
                contextStringBuilder.AppendLine("No references available for this conversation.");
            }

            contextStringBuilder.AppendLine("[/Context]");
            return contextStringBuilder.ToString();
        }

        private static string GetIndexName(ContentReferenceType type) => type switch
        {
            ContentReferenceType.GeneratedDocument => SystemIndexes.GeneratedDocumentContentReferenceIndex,
            ContentReferenceType.GeneratedSection => SystemIndexes.GeneratedSectionContentReferenceIndex,
            ContentReferenceType.ExternalFile => SystemIndexes.ExternalFileContentReferenceIndex,
            ContentReferenceType.ReviewItem => SystemIndexes.ReviewItemContentReferenceIndex,
            ContentReferenceType.ExternalLinkAsset => SystemIndexes.ExternalLinkAssetContentReferenceIndex,
            _ => SystemIndexes.GeneratedDocumentContentReferenceIndex
        };

        /// <summary>
        /// Adds fallback chunks when no vector search results are available
        /// </summary>
        private void AddFallbackChunks(StringBuilder contextStringBuilder, List<ContentReferenceItem> references)
        {
            // Simple fallback to just take the first 100-200 characters from each reference
            foreach (var reference in references.Take(5))
            {
                if (!string.IsNullOrEmpty(reference.RagText))
                {
                    var excerpt = reference.RagText.Length > 500
                        ? reference.RagText.Substring(0, 500) + "..."
                        : reference.RagText;

                    contextStringBuilder.AppendLine($"From {reference.DisplayName ?? "reference"}:");
                    contextStringBuilder.AppendLine(excerpt);
                    contextStringBuilder.AppendLine();
                }
            }
        }
    }
}
