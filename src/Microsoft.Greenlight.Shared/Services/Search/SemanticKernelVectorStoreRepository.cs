// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Greenlight.Shared.Services.Search.Internal;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.FileStorage;

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
    private readonly IFileUrlResolverService _fileUrlResolver;

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
        IFileUrlResolverService fileUrlResolver,
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
        _fileUrlResolver = fileUrlResolver;
    }

    /// <inheritdoc />
    public async Task StoreContentAsync(string documentLibraryName, string indexName, Stream fileStream, string fileName, string? documentReference, string? userId = null, Dictionary<string, string>? additionalTags = null)
    {
        indexName = NormalizeIndex(indexName);
        fileName = System.Net.WebUtility.UrlDecode(fileName);
        fileName = SanitizeFileName(fileName);
        // documentReference is now an identifier, not a URL, so no need to URL encode
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
        var now = DateTimeOffset.UtcNow; 
        var partition = 0;

        // Process all chunks with smart re-chunking on embedding failures
        for (int ci = 0; ci < chunkPositions.Count; ci++)
        {
            var chunk = chunkPositions[ci];
            var subChunks = await ProcessChunkWithRetryAsync(documentLibraryName, chunk.Text, effectiveChunkSize, effectiveChunkOverlap);
            
            foreach (var subChunk in subChunks)
            {
                var tags = BuildTags(documentLibraryName, documentId, fileName, documentReference, userId, additionalTags);
                if (isPdf && chunk.StartPage.HasValue)
                {
                    // Store starting page number as agreed tag name
                    tags["SourceDocumentSourcePage"] = [chunk.StartPage.Value.ToString()];
                }
                chunkRecords.Add(new SkVectorChunkRecord
                {
                    DocumentId = documentId,
                    FileName = fileName,
                    DisplayFileName = Path.GetFileName(fileName), // Extract user-friendly filename for display
                    FileAcknowledgmentRecordId = null, // TODO: Pass through FileAcknowledgmentRecordId when available
                    DocumentReference = documentReference, // Store document reference instead of URL
                    ChunkText = subChunk.Text,
                    Embedding = subChunk.Embedding,
                    PartitionNumber = partition++,
                    IngestedAt = now,
                    Tags = tags
                });
            }
            
            var processed = ci + 1;
            if (nextMilestoneIndex < progressMilestones.Count && processed >= progressMilestones[nextMilestoneIndex])
            {
                var percent = (int)Math.Round((processed / (double)chunkPositions.Count) * 100, MidpointRounding.AwayFromZero);
                _logger.LogInformation("[VectorStore] Embedding progress {Percent}% ({Processed}/{Total}) for file {File} (index={Index})", percent, processed, chunkPositions.Count, fileName, indexName);
                nextMilestoneIndex++;
            }
        }
        
        if (chunkPositions.Count > 0) 
        {
            _logger.LogInformation("[VectorStore] Completed embedding generation for {ChunkCount} original chunks, resulting in {FinalChunkCount} final chunks (file={File}, index={Index})", chunkPositions.Count, chunkRecords.Count, fileName, indexName);
        }
        
        // 5) Upsert the records (collection already exists with correct dimensions)
        await _provider.UpsertAsync(indexName, chunkRecords);
        _logger.LogInformation("Ingested {ChunkCount} chunks into vector index {Index} for file {File}", chunkRecords.Count, indexName, fileName);
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
        _logger.LogInformation("Starting search in SemanticKernelVectorStore for library '{LibraryName}', index '{IndexName}', query '{SearchText}'", 
            documentLibraryName, indexName, searchText);

        try
        {
            // Generate query embedding
            var queryEmbedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, searchText);
            _logger.LogDebug("Generated embeddings for search query '{SearchText}' (embedding dimension: {EmbeddingDimension})", 
                searchText, queryEmbedding.Length);

            var maxResults = options.Top <= 0
                ? (_processOptions?.GetEffectiveMaxSearchResults() ?? _options.MaxSearchResults)
                : Math.Max(0, Math.Min(options.Top, _processOptions?.GetEffectiveMaxSearchResults() ?? _options.MaxSearchResults));
            
            var minRelevance = options.MinRelevance > 0 
                ? options.MinRelevance 
                : (_processOptions?.GetEffectiveMinRelevanceScore() ?? _options.MinRelevanceScore);

            _logger.LogDebug("Search parameters: maxResults={MaxResults}, minRelevance={MinRelevance}, hasParameterFilters={HasFilters}", 
                maxResults, minRelevance, options.ParametersExactMatch?.Count > 0);

            var matches = (await _provider.SearchAsync(indexName, queryEmbedding, maxResults, minRelevance, options.ParametersExactMatch, CancellationToken.None)).ToList();
            _logger.LogDebug("Vector search completed for index '{IndexName}': found {MatchCount} matches with minRelevance={MinRelevance}", indexName, matches.Count, minRelevance);

            if (matches.Count == 0) 
            {
                return new List<SourceReferenceItem>();
            }

            // Process matches and return results
            return await ProcessMatchesToSourceReferenceItems(matches, options, indexName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during search in SemanticKernelVectorStore for library '{LibraryName}', index '{IndexName}', query '{SearchText}'", 
                documentLibraryName, indexName, searchText);
            throw;
        }
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
            EnableProgressiveSearch = true,
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
            if (selectedChunks.Count >= 6) break; 
            if (totalChars > 4000) break; 
            selectedChunks.Add(ch); 
            totalChars += ch.Text.Length;
        }
        
        // Simple fallback answer generation without LLM
        var fallback = new StringBuilder(); 
        fallback.AppendLine("(Synthesized answer based on retrieved context)"); 
        foreach (var c in selectedChunks) 
        {
            fallback.AppendLine("- " + Truncate(c.Text, 300)); 
        }
        var llmAnswer = fallback.ToString();
        
        var answer = new DocumentRepositoryAnswer { Result = llmAnswer, Relevance = selectedChunks.Max(c => c.Relevance), RelevantSources = [] };
        foreach (var group in selectedChunks.GroupBy(c => c.Tags.TryGetValue("DocumentId", out var _) ? c.Tags["DocumentId"].FirstOrDefault() : string.Empty))
        {
            var citation = new DocumentCitation { DocumentId = group.Key ?? string.Empty, FileId = group.First().Tags.TryGetValue("FileName", out var fList) ? fList.FirstOrDefault() ?? string.Empty : string.Empty, Index = indexName };
            foreach (var ch in group) citation.Partitions.Add(ch); 
            answer.RelevantSources.Add(citation);
        }
        return answer;
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
                FileName = g.First().Record.DisplayFileName ?? g.First().Record.FileName,
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

            // Resolve document URL dynamically from DocumentReference
            string? resolvedUrl = null;
            var firstChunk = baseChunks.FirstOrDefault();
            if (!string.IsNullOrEmpty(firstChunk?.DocumentReference))
            {
                try
                {
                    // Extract document ID from reference (format: "doc:{documentId}")
                    if (firstChunk.DocumentReference.StartsWith("doc:", StringComparison.OrdinalIgnoreCase))
                    {
                        var documentIdStr = firstChunk.DocumentReference.Substring(4);
                        if (Guid.TryParse(documentIdStr, out var documentId))
                        {
                            resolvedUrl = await _fileUrlResolver.ResolveUrlByDocumentIdAsync(documentId);
                        }
                    }
                    else
                    {
                        // Fallback: try resolving by vector store document ID and index
                        resolvedUrl = await _fileUrlResolver.ResolveUrlByVectorStoreIdAsync(g.Key, indexName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to resolve URL for document reference {DocumentReference}", firstChunk.DocumentReference);
                }
            }

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
                
                // Use resolved URL in chunk tags for backward compatibility
                var tags = record.Tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
                if (!string.IsNullOrEmpty(resolvedUrl))
                {
                    tags["OriginalDocumentUrl"] = new List<string?> { resolvedUrl };
                }
                
                unified.Chunks.Add(new DocumentChunk
                {
                    Text = record.ChunkText,
                    Relevance = relevance,
                    PartitionNumber = record.PartitionNumber,
                    SizeInBytes = record.ChunkText.Length,
                    Tags = tags,
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
    /// Processes a chunk with automatic retry and smart re-chunking if the embedding service rejects it due to size.
    /// </summary>
    private async Task<List<(string Text, float[] Embedding)>> ProcessChunkWithRetryAsync(string documentLibraryName, string chunkText, int originalChunkSize, int originalChunkOverlap)
    {
        try
        {
            // Try to embed the original chunk
            var embedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, chunkText);
            return new List<(string Text, float[] Embedding)> { (chunkText, embedding) };
        }
        catch (EmbeddingInputTooLargeException ex)
        {
            _logger.LogInformation("Chunk too large for embedding (length={Length}), attempting smart re-chunking with reduced size", chunkText.Length);
            
            // Calculate new chunk size - reduce by 40% to ensure we're well under the limit
            var newChunkSize = Math.Max(100, (int)(originalChunkSize * 0.6));
            var newChunkOverlap = Math.Min(originalChunkOverlap, newChunkSize / 4); // Overlap should be reasonable relative to new size
            
            _logger.LogDebug("Re-chunking with newChunkSize={NewChunkSize}, newChunkOverlap={NewChunkOverlap}", newChunkSize, newChunkOverlap);
            
            // Re-chunk the text with smaller size
            var subChunkStrings = _textChunkingService.ChunkText(chunkText, newChunkSize, newChunkOverlap);
            var results = new List<(string Text, float[] Embedding)>();
            
            foreach (var subChunk in subChunkStrings)
            {
                try
                {
                    var subEmbedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, subChunk);
                    results.Add((subChunk, subEmbedding));
                }
                catch (EmbeddingInputTooLargeException subEx)
                {
                    // If even the sub-chunk is too large, try one more time with even smaller chunks
                    _logger.LogWarning("Sub-chunk still too large (length={Length}), attempting final re-chunking", subChunk.Length);
                    
                    var finalChunkSize = Math.Max(50, newChunkSize / 2);
                    var finalChunkOverlap = Math.Min(newChunkOverlap, finalChunkSize / 4);
                    
                    var finalChunkStrings = _textChunkingService.ChunkText(subChunk, finalChunkSize, finalChunkOverlap);
                    
                    foreach (var finalChunk in finalChunkStrings)
                    {
                        try
                        {
                            var finalEmbedding = await GenerateEmbeddingForDocumentAsync(documentLibraryName, finalChunk);
                            results.Add((finalChunk, finalEmbedding));
                        }
                        catch (EmbeddingInputTooLargeException finalEx)
                        {
                            // If we still can't embed it, log error and skip this chunk
                            _logger.LogError(finalEx, "Final chunk still too large (length={Length}), skipping this chunk", finalChunk.Length);
                        }
                    }
                }
            }
            
            if (results.Count == 0)
            {
                _logger.LogError("Failed to create any embeddings for chunk after re-chunking, original length={Length}", chunkText.Length);
                throw new InvalidOperationException($"Unable to create embeddings for chunk of length {chunkText.Length} even after multiple re-chunking attempts");
            }
            
            _logger.LogInformation("Successfully re-chunked oversized chunk into {SubChunkCount} smaller chunks", results.Count);
            return results;
        }
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

    /// <summary>
    /// Normalizes an index / collection name for consistent provider usage.
    /// </summary>
    private static string NormalizeIndex(string index) => index.Trim().ToLowerInvariant();

    /// <summary>
    /// Builds the tag dictionary applied to each ingested chunk.
    /// </summary>
    private static Dictionary<string, List<string?>> BuildTags(string library, string documentId, string fileName, string? documentReference, string? userId, Dictionary<string, string>? additional)
    {
        var dict = new Dictionary<string, List<string?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DocumentLibrary"] = [library],
            ["FileName"] = [fileName],
            ["DocumentId"] = [documentId],
            ["DocumentProcessName"] = [library],
            ["IsDocumentLibraryDocument"] = [library.StartsWith(AdditionalLibraryPrefix, StringComparison.OrdinalIgnoreCase).ToString().ToLowerInvariant()]
        };
        if (!string.IsNullOrWhiteSpace(documentReference)) dict["DocumentReference"] = [documentReference];
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
    private static string Truncate(string text, int max) => text.Length <= max ? text : text.Substring(0, max) + "�";

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