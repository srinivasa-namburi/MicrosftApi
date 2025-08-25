// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions; // for repository/vector store abstractions
using Microsoft.Extensions.DependencyInjection; // For GetRequiredService
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Greenlight.Shared.Services.Search.Internal;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Shared.Services.Search;

/// <summary>
/// Implementation of <see cref="IDocumentRepository"/> backed by the Semantic Kernel Vector Store provider abstraction.
/// Responsible for: ingest (chunk + embed + upsert), vector search with optional parameter filtering, neighbor expansion
/// (for document processes), and optional answer synthesis via an <see cref="IKernelFactory"/>.
/// </summary>
#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010
public class SemanticKernelVectorStoreRepository : IDocumentRepository
{
    private const string AdditionalLibraryPrefix = "Additional-";
    private const string ReviewsLibraryName = "Reviews";
    private readonly ILogger<SemanticKernelVectorStoreRepository> _logger;
    private readonly VectorStoreOptions _options;
    private readonly IAiEmbeddingService _embeddingService;
    private readonly ISemanticKernelVectorStoreProvider _provider;
    private readonly ITextExtractionService _textExtractionService;
    private readonly ITextChunkingService _textChunkingService;
    private readonly VectorStoreDocumentProcessOptions? _processOptions;
    private readonly IKernelFactory? _kernelFactory;
    private readonly DocumentLibraryType? _documentLibraryType;

    /// <summary>
    /// Creates a new repository instance.
    /// </summary>
    public SemanticKernelVectorStoreRepository(
        ILogger<SemanticKernelVectorStoreRepository> logger,
        IOptionsSnapshot<ServiceConfigurationOptions> rootOptions,
        IAiEmbeddingService embeddingService,
        ISemanticKernelVectorStoreProvider provider,
        ITextExtractionService textExtractionService,
        ITextChunkingService textChunkingService,
        VectorStoreDocumentProcessOptions? processOptions = null,
        IKernelFactory? kernelFactory = null,
        DocumentLibraryType? documentLibraryType = null)
    {
        _logger = logger;
        _options = rootOptions.Value.GreenlightServices.VectorStore;
        _embeddingService = embeddingService;
        _provider = provider;
        _textExtractionService = textExtractionService;
        _textChunkingService = textChunkingService;
        _processOptions = processOptions;
        _kernelFactory = kernelFactory;
        _documentLibraryType = documentLibraryType;
    }

    /// <inheritdoc />
    public async Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName, string? documentUrl, string? userId = null, Dictionary<string, string>? additionalTags = null)
    {
        indexName = NormalizeIndex(indexName);
        fileName = System.Net.WebUtility.UrlDecode(fileName);
        fileName = SanitizeFileName(fileName);
        if (!string.IsNullOrWhiteSpace(documentUrl)) documentUrl = System.Net.WebUtility.UrlEncode(documentUrl);
        var fullText = await _textExtractionService.ExtractTextAsync(fileStream, fileName);
        var effectiveChunkSize = _processOptions?.GetEffectiveChunkSize() ?? (_options.ChunkSize <= 0 ? 1000 : _options.ChunkSize);
        var effectiveChunkOverlap = _processOptions?.GetEffectiveChunkOverlap() ?? (_options.ChunkOverlap <= 0 ? 100 : _options.ChunkOverlap);

        // Build page map (start indices) when PDF so we can tag starting page for a chunk without constraining chunking.
        var isPdf = fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        List<int> pageStarts = new() { 0 }; // page 1 starts at 0
        if (isPdf)
        {
            for (int i = 0; i < fullText.Length; i++)
            {
                if (fullText[i] == '\f')
                {
                    // Next page starts after the form-feed character
                    pageStarts.Add(i + 1);
                }
            }
        }

        // 1) Chunk across the full text (not page-bounded), preserving overlap
        var chunkStrings = _textChunkingService.ChunkText(fullText, effectiveChunkSize, effectiveChunkOverlap);

        // 2) Compute approximate starting index for each chunk so we can resolve a starting page
        var chunkPositions = new List<(string Text, int StartIndex, int? StartPage)>();
        int searchFrom = 0;
        foreach (var c in chunkStrings)
        {
            // Try to find the exact occurrence of the chunk from the running position
            var idx = fullText.IndexOf(c, searchFrom, StringComparison.Ordinal);
            if (idx < 0)
            {
                // If verbatim chunk isn't found (e.g., whitespace normalization during chunking),
                // probe using a small prefix near the expected position. If still not found, fall back
                // to the running position instead of 0, so the computed page is approximately correct.
                var probeLen = Math.Min(64, c.Length);
                var prefix = c.AsSpan(0, probeLen).ToString();
                var probeIdx = fullText.IndexOf(prefix, searchFrom, StringComparison.Ordinal);
                if (probeIdx >= 0)
                {
                    idx = probeIdx;
                }
                else
                {
                    idx = Math.Clamp(searchFrom, 0, Math.Max(0, fullText.Length - 1));
                }
            }

            int? page = null;
            if (isPdf && pageStarts.Count > 1)
            {
                // Find the page whose start is <= idx (linear scan is fine; counts are small). Use binary search if needed later.
                int p = 0;
                while (p + 1 < pageStarts.Count && pageStarts[p + 1] <= idx) p++;
                page = p + 1; // 1-based page number
            }

            chunkPositions.Add((c, idx, page));

            // Advance searchFrom taking overlap into account
            var advance = Math.Max(1, c.Length - effectiveChunkOverlap);
            searchFrom = Math.Min(fullText.Length, idx + advance);
        }

        _logger.LogDebug("Chunking document {FileName} with chunk size {ChunkSize} and overlap {ChunkOverlap}, produced {ChunkCount} chunks", fileName, effectiveChunkSize, effectiveChunkOverlap, chunkPositions.Count);
        
        // Early return if no chunks to process
        if (chunkPositions.Count == 0)
        {
            _logger.LogInformation("No chunks generated from document {FileName}, skipping ingestion", fileName);
            return;
        }

        // 3) Resolve embedding dimensions efficiently using context-aware resolution
        _logger.LogDebug("Resolving embedding dimensions for document library or process '{Name}' (context: {DocumentLibraryType})", documentLibraryName, _documentLibraryType);
        int embeddingDimensions;
        try
        {
            embeddingDimensions = await ResolveEmbeddingDimensionsAsync(documentLibraryName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve embedding config for '{DocumentLibraryName}', using default dimensions", documentLibraryName);
            // Final fallback to configured or default dimensions
            embeddingDimensions = _options.VectorSize > 0 ? _options.VectorSize : 1536;
            _logger.LogDebug("Using fallback embedding dimensions: {Dimensions}", embeddingDimensions);
        }

        // 4) Ensure collection exists with the correct dimensions BEFORE generating all embeddings
        await _provider.EnsureCollectionAsync(indexName, embeddingDimensions);

        _logger.LogInformation("[VectorStore] Starting embedding generation for {ChunkCount} chunks (file={File}, index={Index})", chunkPositions.Count, fileName, indexName);
        var progressMilestones = new List<int>();
        if (chunkPositions.Count > 0)
        {
            for (int p = 20; p <= 100; p += 20)
            {
                var at = (int)Math.Ceiling(chunkPositions.Count * (p / 100.0));
                if (at > 0 && at <= chunkPositions.Count && !progressMilestones.Contains(at)) progressMilestones.Add(at);
            }
        }
        int nextMilestoneIndex = 0;
        // Use URL-safe Base64 encoding for document identifiers to satisfy Azure AI Search key constraints across providers
        var documentId = Base64UrlEncode(fileName);
        var chunkRecords = new List<SkVectorChunkRecord>();
        var now = DateTimeOffset.UtcNow; var partition = 0;
        foreach (var chunk in chunkPositions)
        {
            var embedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, chunk.Text);
            var tags = BuildTags(documentLibraryName, documentId, fileName, documentUrl, userId, additionalTags);
            if (isPdf && chunk.StartPage.HasValue)
            {
                // Store starting page number as agreed tag name
                tags["SourceDocumentSourcePage"] = [chunk.StartPage.Value.ToString()];
            }
            chunkRecords.Add(new SkVectorChunkRecord
            {
                DocumentId = documentId,
                FileName = fileName,
                OriginalDocumentUrl = documentUrl,
                ChunkText = chunk.Text,
                Embedding = embedding,
                PartitionNumber = partition++,
                IngestedAt = now,
                Tags = tags
            });
            var processed = chunkRecords.Count;
            if (nextMilestoneIndex < progressMilestones.Count && processed >= progressMilestones[nextMilestoneIndex])
            {
                var percent = (int)Math.Round((processed / (double)chunkPositions.Count) * 100, MidpointRounding.AwayFromZero);
                _logger.LogInformation("[VectorStore] Embedding progress {Percent}% ({Processed}/{Total}) for file {File} (index={Index})", percent, processed, chunkPositions.Count, fileName, indexName);
                nextMilestoneIndex++;
            }
        }
        if (chunkPositions.Count > 0) _logger.LogInformation("[VectorStore] Completed embedding generation for {ChunkCount} chunks (file={File}, index={Index})", chunkPositions.Count, fileName, indexName);
        
        // 5) Upsert the records (collection already exists with correct dimensions)
        await _provider.UpsertAsync(indexName, chunkRecords);
        _logger.LogInformation("Ingested {ChunkCount} chunks into vector index {Index} for file {File}", chunkPositions.Count, indexName, fileName);
    }

    /// <summary>
    /// Context-aware embedding dimension resolution based on the repository type.
    /// </summary>
    private async Task<int> ResolveEmbeddingDimensionsAsync(string documentLibraryName)
    {
        // Use context-aware resolution order based on the repository type
        if (_documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
        {
            // For document processes, try document process first, then document library
            try
            {
                var (_, processDimensions) = await _embeddingService.ResolveEmbeddingConfigForDocumentProcessAsync(documentLibraryName);
                _logger.LogDebug("Resolved embedding dimensions from document process '{DocumentProcess}': {Dimensions}", documentLibraryName, processDimensions);
                return processDimensions;
            }
            catch (Exception exProcess)
            {
                _logger.LogDebug(exProcess, "Failed to resolve embedding config as document process '{DocumentProcess}', trying as document library", documentLibraryName);
                try
                {
                    var (_, libraryDimensions) = await _embeddingService.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName);
                    _logger.LogDebug("Resolved embedding dimensions from document library '{DocumentLibrary}': {Dimensions}", documentLibraryName, libraryDimensions);
                    return libraryDimensions;
                }
                catch (Exception exLibrary)
                {
                    _logger.LogDebug(exLibrary, "Failed to resolve embedding config as document library '{DocumentLibrary}' after document process failure", documentLibraryName);
                    throw;
                }
            }
        }
        else
        {
            // For document libraries (or unknown context), try document library first, then document process
            try
            {
                var (_, libraryDimensions) = await _embeddingService.ResolveEmbeddingConfigForDocumentLibraryAsync(documentLibraryName);
                _logger.LogDebug("Resolved embedding dimensions from document library '{DocumentLibrary}': {Dimensions}", documentLibraryName, libraryDimensions);
                return libraryDimensions;
            }
            catch (Exception exLibrary)
            {
                _logger.LogDebug(exLibrary, "Failed to resolve embedding config as document library '{DocumentLibrary}', trying as document process", documentLibraryName);
                try
                {
                    var (_, processDimensions) = await _embeddingService.ResolveEmbeddingConfigForDocumentProcessAsync(documentLibraryName);
                    _logger.LogDebug("Resolved embedding dimensions from document process '{DocumentProcess}': {Dimensions}", documentLibraryName, processDimensions);
                    return processDimensions;
                }
                catch (Exception exProcess)
                {
                    _logger.LogDebug(exProcess, "Failed to resolve embedding config as document process '{DocumentProcess}' after document library failure", documentLibraryName);
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Context-aware embedding generation based on the repository type.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingForDocumentAsync(string documentLibraryName, string text)
    {
        // Use context-aware embedding generation based on the repository type
        if (_documentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary)
        {
            // For document processes, use document process embedding service
            return await _embeddingService.GenerateEmbeddingsForDocumentProcessAsync(documentLibraryName, text);
        }
        else
        {
            // For document libraries, use document library embedding service
            return await _embeddingService.GenerateEmbeddingsForDocumentLibraryAsync(documentLibraryName, text);
        }
    }

    /// <inheritdoc />
    public async Task DeleteContentAsync(string documentLibraryName, string indexName, string fileName)
    {
        indexName = NormalizeIndex(indexName);
        await _provider.DeleteFileAsync(indexName, fileName);
    }

    /// <inheritdoc />
    public async Task<List<SourceReferenceItem>> SearchAsync(string documentLibraryName, string searchText, ConsolidatedSearchOptions options)
    {
        var indexName = NormalizeIndex(options.IndexName ?? documentLibraryName);
        _logger.LogInformation("Starting search in SemanticKernelVectorStore for library '{LibraryName}', index '{IndexName}', query '{SearchText}', progressiveSearch={ProgressiveSearch}", 
            documentLibraryName, indexName, searchText, options.EnableProgressiveSearch);

        try
        {
            // Try initial search with specified relevance threshold
            var initialResults = await PerformSingleSearchAsync(documentLibraryName, searchText, options, indexName);
            
            if (initialResults.Count > 0)
            {
                _logger.LogInformation("Initial search found {ResultCount} results for index '{IndexName}' with minRelevance={MinRelevance}", 
                    initialResults.Count, indexName, options.MinRelevance);
                return initialResults;
            }

            // If no results and progressive search is disabled, return empty
            if (!options.EnableProgressiveSearch)
            {
                _logger.LogInformation("No results found and progressive search is disabled for index '{IndexName}'", indexName);
                return new List<SourceReferenceItem>();
            }

            _logger.LogInformation("Initial search found no results, starting progressive search for index '{IndexName}'", indexName);
            
            // Determine if we should use progressive search based on chunk size
            var shouldUseProgressiveSearch = ShouldUseProgressiveSearchForChunkSize();
            if (shouldUseProgressiveSearch)
            {
                _logger.LogInformation("Large chunk size detected ({ChunkSize} tokens), progressive search recommended for index '{IndexName}'", 
                    _processOptions?.GetEffectiveChunkSize() ?? _options.ChunkSize, indexName);
            }

            // Progressive search: try broader query first
            var broaderQuery = ExtractBroaderQuery(searchText);
            if (broaderQuery != searchText)
            {
                _logger.LogDebug("Trying broader search with query '{BroaderQuery}' for index '{IndexName}'", broaderQuery, indexName);
                
                var broaderOptions = CreateModifiedSearchOptions(options, broaderQuery: true);
                var broaderResults = await PerformSingleSearchAsync(documentLibraryName, broaderQuery, broaderOptions, indexName);
                
                if (broaderResults.Count > 0)
                {
                    _logger.LogInformation("Broader search found {ResultCount} results for index '{IndexName}'", broaderResults.Count, indexName);
                    return broaderResults;
                }
            }

            // Progressive relevance threshold search
            var thresholds = options.ProgressiveRelevanceThresholds ?? GetDefaultProgressiveThresholds();
            foreach (var threshold in thresholds)
            {
                if (threshold >= options.MinRelevance)
                    continue; // Skip thresholds that are higher than or equal to the original

                _logger.LogDebug("Trying progressive search with threshold {Threshold} for index '{IndexName}'", threshold, indexName);
                
                var progressiveOptions = CreateModifiedSearchOptions(options, newRelevanceThreshold: threshold);
                var progressiveResults = await PerformSingleSearchAsync(documentLibraryName, broaderQuery, progressiveOptions, indexName);
                
                if (progressiveResults.Count > 0)
                {
                    _logger.LogInformation("Progressive search with threshold {Threshold} found {ResultCount} results for index '{IndexName}'", 
                        threshold, progressiveResults.Count, indexName);
                    return progressiveResults;
                }
            }

            // Keyword-based fallback search
            if (options.EnableKeywordFallback)
            {
                var keywordResults = await PerformKeywordFallbackSearch(documentLibraryName, searchText, options, indexName);
                if (keywordResults.Count > 0)
                {
                    return keywordResults;
                }
            }

            _logger.LogWarning("All progressive search tiers exhausted for index '{IndexName}'. No relevant content found.", indexName);
            return new List<SourceReferenceItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search in SemanticKernelVectorStore for library '{LibraryName}', index '{IndexName}', query '{SearchText}'", 
                documentLibraryName, indexName, searchText);
            throw;
        }
    }

    /// <summary>
    /// Performs a single search operation without progressive fallbacks.
    /// </summary>
    private async Task<List<SourceReferenceItem>> PerformSingleSearchAsync(string documentLibraryName, string searchText, ConsolidatedSearchOptions options, string indexName)
    {
        var queryEmbedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, searchText);
        _logger.LogDebug("Generated embeddings for search query '{SearchText}' (embedding dimension: {EmbeddingDimension})", 
            searchText, queryEmbedding.Length);

        var maxResults = options.Top <= 0
            ? (_processOptions?.GetEffectiveMaxSearchResults() ?? _options.MaxSearchResults)
            : Math.Max(0, Math.Min(options.Top, _processOptions?.GetEffectiveMaxSearchResults() ?? _options.MaxSearchResults));
        
        // FIXED: Respect the MinRelevance passed in options instead of always using defaults
        var minRelevance = options.MinRelevance > 0 
            ? options.MinRelevance 
            : (_processOptions?.GetEffectiveMinRelevanceScore() ?? _options.MinRelevanceScore);

        _logger.LogDebug("Search parameters: maxResults={MaxResults}, minRelevance={MinRelevance} (from options={OptionsMinRelevance}, effective={EffectiveMinRelevance}), hasParameterFilters={HasFilters}", 
            maxResults, minRelevance, options.MinRelevance, _processOptions?.GetEffectiveMinRelevanceScore() ?? _options.MinRelevanceScore, options.ParametersExactMatch?.Count > 0);

        List<VectorSearchMatch> matches;
        try
        {
            matches = (await _provider.SearchAsync(indexName, queryEmbedding, maxResults, minRelevance, options.ParametersExactMatch, CancellationToken.None)).ToList();
            _logger.LogDebug("Vector search completed for index '{IndexName}': found {MatchCount} matches with minRelevance={MinRelevance}", indexName, matches.Count, minRelevance);
        }
        catch (ArgumentException ex) 
        { 
            _logger.LogError(ex, "Invalid search parameters for index '{IndexName}', query '{SearchText}'", indexName, searchText);
            throw new VectorStoreException(VectorStoreErrorReason.InvalidFilter, "Invalid search parameters", ex); 
        }
        catch (TimeoutException ex) 
        { 
            _logger.LogError(ex, "Vector store search timed out for index '{IndexName}', query '{SearchText}'", indexName, searchText);
            throw new VectorStoreException(VectorStoreErrorReason.Timeout, "Vector store search timed out", ex); 
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Vector store provider failure for index '{IndexName}', query '{SearchText}'", indexName, searchText);
            throw new VectorStoreException(VectorStoreErrorReason.ProviderUnavailable, "Vector store provider failure", ex); 
        }

        if (matches.Count == 0) 
        {
            return new List<SourceReferenceItem>();
        }

        // Client-side parameter exact match filtering (until provider supports server-side filters)
        if (options.ParametersExactMatch is { Count: > 0 })
        {
            var originalCount = matches.Count;
            matches = matches
                .Where(m => options.ParametersExactMatch.All(f => m.Record.Tags.TryGetValue(f.Key, out var vals) && vals.Any(v => string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase))))
                .ToList();
            _logger.LogDebug("Applied client-side parameter filtering: {OriginalCount} -> {FilteredCount} matches", originalCount, matches.Count);

            if (matches.Count == 0) 
            {
                return new List<SourceReferenceItem>();
            }
        }

        // Process matches and return results
        return await ProcessMatchesToSourceReferenceItems(matches, options, indexName);
    }

    /// <summary>
    /// Processes vector search matches into source reference items.
    /// </summary>
    private async Task<List<SourceReferenceItem>> ProcessMatchesToSourceReferenceItems(List<VectorSearchMatch> matches, ConsolidatedSearchOptions options, string indexName)
    {
        for (int i = 0; i < matches.Count; i++)
        {
            var current = matches[i]; 
            var norm = ScoreNormalizer.Normalize(current.Score); 
            matches[i] = new VectorSearchMatch(current.Record, norm);
        }
        
        matches = matches
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Record.PartitionNumber)
            .ThenBy(m => m.Record.DocumentId)
            .ToList();

        var preceding = options.PrecedingPartitionCount;
        var following = options.FollowingPartitionCount;
        bool isDocumentProcess = options.DocumentLibraryType == DocumentLibraryType.PrimaryDocumentProcessLibrary;

        _logger.LogDebug("Processing matches with neighbor expansion: preceding={Preceding}, following={Following}, isDocumentProcess={IsDocumentProcess}", 
            preceding, following, isDocumentProcess);

        var grouped = matches.GroupBy(m => m.Record.DocumentId);
        var results = new List<SourceReferenceItem>();
        
        foreach (var g in grouped)
        {
            var unified = new VectorStoreAggregatedSourceReferenceItem
            {
                IndexName = indexName,
                FileName = g.First().Record.FileName,
                DocumentId = g.Key,
                Score = g.Max(x => x.Score)
            };
            unified.SetBasicParameters();
            var baseChunks = g.Select(x => x.Record).OrderBy(r => r.PartitionNumber).ToList();
            var allChunks = new Dictionary<int, SkVectorChunkRecord>();
            foreach (var b in baseChunks) allChunks[b.PartitionNumber] = b;

            if (isDocumentProcess && (preceding > 0 || following > 0))
            {
                foreach (var chunk in baseChunks)
                {
                    var neighbors = await _provider.GetNeighborChunksAsync(indexName, g.Key, chunk.PartitionNumber, preceding, following);
                    foreach (var n in neighbors)
                    {
                        allChunks.TryAdd(n.PartitionNumber, n);
                    }
                }
            }

            // Persist only identifiers for lazy loading later
            unified.StoredPartitionNumbers = string.Join(",", allChunks.Keys.OrderBy(k => k));

            foreach (var record in allChunks.Values.OrderBy(c => c.PartitionNumber))
            {
                double relevance;
                var direct = g.FirstOrDefault(m => m.Record.PartitionNumber == record.PartitionNumber);
                if (direct != null) 
                    relevance = direct.Score;
                else
                {
                    var nearest = baseChunks.OrderBy(b => Math.Abs(b.PartitionNumber - record.PartitionNumber)).First();
                    relevance = g.First(m => m.Record.PartitionNumber == nearest.PartitionNumber).Score;
                }
                
                unified.Chunks.Add(new DocumentChunk
                {
                    Text = record.ChunkText,
                    Relevance = relevance,
                    PartitionNumber = record.PartitionNumber,
                    SizeInBytes = record.ChunkText.Length,
                    Tags = record.Tags,
                    LastUpdate = record.IngestedAt
                });
            }
            unified.SourceOutput = string.Join("\n", unified.Chunks.Select(c => c.Text));
            results.Add(unified);
        }

        _logger.LogInformation("Search completed successfully for index '{IndexName}': returning {ResultCount} aggregated results from {TotalChunkCount} chunks", 
            indexName, results.Count, results.Cast<VectorStoreAggregatedSourceReferenceItem>().Sum(r => r.Chunks.Count));

        return results.Cast<SourceReferenceItem>().ToList();
    }

    /// <summary>
    /// Determines if progressive search should be used based on chunk size.
    /// </summary>
    private bool ShouldUseProgressiveSearchForChunkSize()
    {
        var chunkSize = _processOptions?.GetEffectiveChunkSize() ?? _options.ChunkSize;
        return chunkSize > 1200; // Progressive search recommended for chunks larger than 1200 tokens
    }

    /// <summary>
    /// Gets default progressive relevance thresholds.
    /// </summary>
    private static double[] GetDefaultProgressiveThresholds()
    {
        return new[] { 0.3, 0.2, 0.15, 0.1, 0.05 };
    }

    /// <summary>
    /// Extracts a broader query from the original search text.
    /// </summary>
    private static string ExtractBroaderQuery(string originalQuery)
    {
        // Simple strategy: if query contains section numbers or multiple terms, 
        // try with just the meaningful terms
        var parts = originalQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        // Remove section numbers and short words to create broader query
        var meaningfulParts = parts
            .Where(part => part.Length > 3 && !IsNumericSection(part))
            .ToArray();
            
        return meaningfulParts.Length > 0 ? string.Join(" ", meaningfulParts) : originalQuery;
    }

    /// <summary>
    /// Checks if a text part appears to be a numeric section identifier.
    /// </summary>
    private static bool IsNumericSection(string part)
    {
        return part.All(c => char.IsDigit(c) || c == '.');
    }

    /// <summary>
    /// Creates modified search options for progressive search tiers.
    /// </summary>
    private static ConsolidatedSearchOptions CreateModifiedSearchOptions(ConsolidatedSearchOptions original, 
        bool broaderQuery = false, double? newRelevanceThreshold = null)
    {
        return new ConsolidatedSearchOptions
        {
            DocumentLibraryType = original.DocumentLibraryType,
            IndexName = original.IndexName,
            Top = broaderQuery ? Math.Max(original.Top, 15) : original.Top, // Increase breadth for broader queries
            MinRelevance = newRelevanceThreshold ?? original.MinRelevance,
            PrecedingPartitionCount = original.PrecedingPartitionCount,
            FollowingPartitionCount = original.FollowingPartitionCount,
            ParametersExactMatch = original.ParametersExactMatch,
            EnableProgressiveSearch = false, // Prevent recursive progressive search
            EnableKeywordFallback = false
        };
    }

    /// <summary>
    /// Performs keyword-based fallback search using extracted terms.
    /// </summary>
    private async Task<List<SourceReferenceItem>> PerformKeywordFallbackSearch(string documentLibraryName, string searchText, ConsolidatedSearchOptions options, string indexName)
    {
        try
        {
            var keyTerms = ExtractKeyTerms(searchText);
            if (!keyTerms.Any())
            {
                return new List<SourceReferenceItem>();
            }

            _logger.LogInformation("Starting keyword fallback search for index '{IndexName}' with {TermCount} key terms", indexName, keyTerms.Count);

            foreach (var term in keyTerms.Take(3)) // Try top 3 key terms
            {
                var termOptions = new ConsolidatedSearchOptions
                {
                    DocumentLibraryType = options.DocumentLibraryType,
                    IndexName = options.IndexName,
                    Top = 5, // Smaller result set for individual terms
                    MinRelevance = 0.05, // Very low threshold for key terms
                    PrecedingPartitionCount = options.PrecedingPartitionCount,
                    FollowingPartitionCount = options.FollowingPartitionCount,
                    ParametersExactMatch = options.ParametersExactMatch,
                    EnableProgressiveSearch = false, // Prevent recursive calls
                    EnableKeywordFallback = false
                };

                var termResults = await PerformSingleSearchAsync(documentLibraryName, term, termOptions, indexName);
                
                if (termResults.Count > 0)
                {
                    _logger.LogInformation("Keyword fallback search with term '{Term}' found {ResultCount} results for index '{IndexName}'", 
                        term, termResults.Count, indexName);
                    return termResults;
                }
            }

            return new List<SourceReferenceItem>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keyword fallback search failed for index '{IndexName}'", indexName);
            return new List<SourceReferenceItem>();
        }
    }

    /// <summary>
    /// Extracts key terms from search text for fallback searching.
    /// </summary>
    private List<string> ExtractKeyTerms(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<string>();

        // Simple key term extraction - remove common words and short terms
        var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "or", "but", "in", "on", "at", "to", "for", "of", "with", "by", "from", "up", "about", "into", "through", "during", "before", "after", "above", "below", "between", "among", "within", "without", "against", "upon", "across", "beneath", "beside", "beyond", "except", "since", "until", "unless", "whereas", "while", "although", "because", "if", "when", "where", "how", "why", "what", "which", "who", "whom", "whose", "this", "that", "these", "those", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "must", "can", "shall"
        };

        return searchText
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 3 && !commonWords.Contains(word))
            .Select(word => word.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(word => word.Length) // Longer terms first
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DocumentRepositoryAnswer?> AskAsync(string documentLibraryName, string indexName, Dictionary<string, string>? parametersExactMatch, string question)
    {
        var searchOptions = new ConsolidatedSearchOptions
        {
            DocumentLibraryType = DocumentLibraryType.AdditionalDocumentLibrary,
            IndexName = indexName,
            Top = _processOptions?.GetEffectiveMaxSearchResults() ?? _options.MaxSearchResults,
            MinRelevance = _processOptions?.GetEffectiveMinRelevanceScore() ?? _options.MinRelevanceScore,
            ParametersExactMatch = parametersExactMatch ?? new(),
            PrecedingPartitionCount = 0,
            FollowingPartitionCount = 0,
            EnableProgressiveSearch = true, // Enable progressive search for Ask operations
            EnableKeywordFallback = true
        };
        var searchResults = await SearchAsync(documentLibraryName, question, searchOptions);
        if (searchResults.Count == 0) return null;
        var allChunks = searchResults
            .OfType<VectorStoreAggregatedSourceReferenceItem>()
            .SelectMany(r => r.Chunks)
            .OrderByDescending(c => c.Relevance)
            .ToList();
        var selectedChunks = new List<DocumentChunk>();
        var totalChars = 0;
        foreach (var ch in allChunks)
        {
            if (selectedChunks.Count >= 6) break; if (totalChars > 4000) break; selectedChunks.Add(ch); totalChars += ch.Text.Length;
        }
        string llmAnswer = string.Empty; bool usedLlm = false;
        if (_kernelFactory != null)
        {
            try
            {
                Kernel kernel; AzureOpenAIPromptExecutionSettings settings; ChatOptions chatOptions;
                var isAdditional = documentLibraryName.StartsWith(AdditionalLibraryPrefix, StringComparison.OrdinalIgnoreCase);
                var isReviews = documentLibraryName.StartsWith(ReviewsLibraryName, StringComparison.OrdinalIgnoreCase);
                if (isAdditional || isReviews)
                {
                    kernel = await _kernelFactory.GetDefaultGenericKernelAsync();
                    chatOptions = new ChatOptions { Temperature = 0.7f, MaxOutputTokens = 800, FrequencyPenalty = null };
                    settings = new AzureOpenAIPromptExecutionSettings { MaxTokens = chatOptions.MaxOutputTokens ?? 800, Temperature = chatOptions.Temperature, FrequencyPenalty = chatOptions.FrequencyPenalty };
                }
                else
                {
                    try { kernel = await _kernelFactory.GetKernelForDocumentProcessAsync(documentLibraryName); }
                    catch (Exception exLib) { _logger.LogDebug(exLib, "Document process {LibraryName} not found; using generic kernel", documentLibraryName); kernel = await _kernelFactory.GetDefaultGenericKernelAsync(); }
                    try
                    {
                        chatOptions = await _kernelFactory.GetChatOptionsForDocumentProcessAsync(documentLibraryName, AiTaskType.QuestionAnswering);
                        settings = new AzureOpenAIPromptExecutionSettings { MaxTokens = chatOptions.MaxOutputTokens ?? 800, Temperature = chatOptions.Temperature, FrequencyPenalty = chatOptions.FrequencyPenalty };
                    }
                    catch (Exception exSettings)
                    {
                        _logger.LogDebug(exSettings, "Prompt settings for {LibraryName} not available; using fallback settings", documentLibraryName);
                        chatOptions = new ChatOptions { Temperature = 0.7f, MaxOutputTokens = 800, FrequencyPenalty = null };
                        settings = new AzureOpenAIPromptExecutionSettings { MaxTokens = 800, Temperature = chatOptions.Temperature };
                    }
                }
                var systemPrompt = "You are an AI assistant. Answer the user's question using ONLY the provided context chunks. If the answer is not in the context, say you do not have enough information.";
                var sbContext = new StringBuilder();
                for (int i = 0; i < selectedChunks.Count; i++)
                {
                    var c = selectedChunks[i]; sbContext.AppendLine($"[Chunk {i + 1} | Relevance={c.Relevance:F3} | Partition={c.PartitionNumber}]\n{Truncate(c.Text, 1200)}\n");
                }
                var userPrompt = new StringBuilder();
                userPrompt.AppendLine("Question:");
                userPrompt.AppendLine(question.Trim());
                userPrompt.AppendLine();
                userPrompt.AppendLine($"Document Library: {documentLibraryName}");
                userPrompt.AppendLine("Context Chunks:");
                userPrompt.AppendLine(sbContext.ToString());
                userPrompt.AppendLine("Instructions: Provide a concise, factual answer. List cited chunk numbers in parentheses like (Chunks 2,4) if used.");
                var history = new ChatHistory(); history.AddSystemMessage(systemPrompt); history.AddUserMessage(userPrompt.ToString());
                var chatClient = kernel.Services.GetService<IChatClient>();
                if (chatClient != null)
                {
                    var requestMessages = new List<ChatMessage> { new ChatMessage(ChatRole.System, systemPrompt), new ChatMessage(ChatRole.User, userPrompt.ToString()) };
                    var options = new ChatOptions { Temperature = settings.Temperature.HasValue ? (float?)settings.Temperature.Value : null, MaxOutputTokens = settings.MaxTokens };
                    var completion = await chatClient.GetResponseAsync(requestMessages, options); llmAnswer = completion?.Text ?? string.Empty;
                }
                usedLlm = !string.IsNullOrWhiteSpace(llmAnswer);
            }
            catch (Exception ex) { _logger.LogError(ex, "LLM question answering failed; falling back to naive synthesis."); }
        }
        if (!usedLlm)
        {
            var fallback = new StringBuilder(); fallback.AppendLine("(Fallback synthesized answer based on retrieved context)"); foreach (var c in selectedChunks) fallback.AppendLine("- " + Truncate(c.Text, 300)); llmAnswer = fallback.ToString();
        }
        var answer = new DocumentRepositoryAnswer { Result = llmAnswer, Relevance = selectedChunks.Max(c => c.Relevance), RelevantSources = [] };
        foreach (var group in selectedChunks.GroupBy(c => c.Tags.TryGetValue("DocumentId", out var _) ? c.Tags["DocumentId"].FirstOrDefault() : string.Empty))
        {
            var citation = new DocumentCitation { DocumentId = group.Key ?? string.Empty, FileId = group.First().Tags.TryGetValue("FileName", out var fList) ? fList.FirstOrDefault() ?? string.Empty : string.Empty, Index = indexName };
            foreach (var ch in group) citation.Partitions.Add(ch); answer.RelevantSources.Add(citation);
        }
        return answer;
    }

    /// <summary>
    /// Normalizes an index / collection name for consistent provider usage.
    /// </summary>
    private static string NormalizeIndex(string index) => index.Trim().ToLowerInvariant();

    /// <summary>
    /// Builds the tag dictionary applied to each ingested chunk.
    /// </summary>
    private static Dictionary<string, List<string?>> BuildTags(string library, string documentId, string fileName, string? url, string? userId, Dictionary<string, string>? additional)
    {
        var dict = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentLibrary"] = [library],
            ["FileName"] = [fileName],
            ["DocumentId"] = [documentId],
            ["DocumentProcessName"] = [library],
            ["IsDocumentLibraryDocument"] = [library.StartsWith(AdditionalLibraryPrefix, StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()]
        };
        if (!string.IsNullOrWhiteSpace(url)) dict["OriginalDocumentUrl"] = [url];
        if (!string.IsNullOrWhiteSpace(userId)) { dict["UserId"] = [userId]; dict["UploadedByUserOid"] = [userId]; }
        if (additional != null) foreach (var kvp in additional) dict[kvp.Key] = [kvp.Value];
        return dict;
    }

    /// <summary>
    /// Sanitizes a file name to a storage?friendly identifier.
    /// </summary>
    private static string SanitizeFileName(string fileName) => fileName.Replace(" ", "_").Replace("+", "_").Replace("~", "_").Replace("/", "_");

    /// <summary>
    /// Truncates text with an ellipsis when longer than a specified maximum.
    /// </summary>
    private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max) + "…";

    /// <summary>
    /// URL-safe Base64 encode without padding for consistent, AZ Search-friendly identifiers.
    /// </summary>
    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
