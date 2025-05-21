// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;

/// <summary>
/// Plugin for advanced operations on ContentReferenceItems, including retrieval of document text (RAG), metadata, chunking, and semantic search for relevant content.
/// Use these methods to access, analyze, and search within documents or content references in a way that supports retrieval-augmented generation (RAG) and cross-document reasoning.
/// </summary>
public class ContentReferenceLookupPlugin
{
    private readonly IContentReferenceService _contentReferenceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentReferenceLookupPlugin"/> class.
    /// </summary>
    /// <param name="contentReferenceService">The content reference service.</param>
    public ContentReferenceLookupPlugin(IContentReferenceService contentReferenceService)
    {
        _contentReferenceService = contentReferenceService;
    }

    /// <summary>
    /// Retrieves the full RAG (retrieval-augmented generation) text for a given content reference item.
    /// Use this to get the entire text of a document or section for context or direct analysis.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the content reference item (document, section, file, etc.).</param>
    /// <returns>The full RAG text for the reference, or null if not available.</returns>
    [KernelFunction, Description("Get the full text content (RAG text) for a ContentReferenceItem by its unique ID. Use this to retrieve the entire document or section for context or direct analysis. Expensive - should probably be avoided unless absolutely neccessary.")]
    public async Task<string?> GetRagText(
        [Description("The unique identifier of the content reference item (document, section, file, etc.).")] Guid referenceId)
    {
        return await _contentReferenceService.GetRagTextAsync(referenceId);
    }

    /// <summary>
    /// Retrieves metadata for a content reference item, such as display name, description, and type.
    /// Use this to understand what a reference represents before accessing its content.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the content reference item.</param>
    /// <returns>A string containing display name, description, and type, or "Not found" if unavailable.</returns>
    [KernelFunction, Description("Get metadata (display name, description, type) for a ContentReferenceItem by its unique ID. Use this to understand what a reference represents before accessing its content.")]
    public async Task<string> GetMetadata(
        [Description("The unique identifier of the content reference item.")] Guid referenceId)
    {
        var items = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync([referenceId]);
        var item = items.FirstOrDefault();
        if (item == null) return "Not found";
        return $"DisplayName: {item.DisplayName}\nDescription: {item.Description}\nReferenceType: {item.ReferenceType}";
    }

    /// <summary>
    /// Retrieves the precomputed content chunks for a content reference item, aligned with how embeddings are generated for semantic search.
    /// Use this to break a document or section into manageable pieces for fine-grained analysis or to understand chunk boundaries for search.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the content reference item.</param>
    /// <param name="maxTokens">The maximum number of tokens per chunk (default: 1200). Should match the chunking used for embeddings.</param>
    /// <returns>A list of text chunks. Each chunk is a segment of the document or section, suitable for targeted analysis or search.</returns>
    [KernelFunction, Description("Get the precomputed content chunks for a ContentReferenceItem by its unique ID. Use this to break a document or section into manageable pieces for fine-grained analysis or to understand chunk boundaries for semantic search. Chunks are aligned with how embeddings are generated.")]
    public async Task<List<string>> GetChunks(
        [Description("The unique identifier of the content reference item.")] Guid referenceId,
        [Description("The maximum number of tokens per chunk (default: 1200). Should match the chunking used for embeddings.")] int maxTokens = 1200)
    {
        var items = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync([referenceId]);
        var item = items.FirstOrDefault();
        if (item == null || string.IsNullOrEmpty(item.RagText)) return new List<string>();

        // Get the chunks from the embeddings table to ensure alignment
        var embeddingsDict = await _contentReferenceService.GetOrCreateEmbeddingsForContentAsync([item], maxTokens);
        var chunks = embeddingsDict
            .Where(e => e.Key.ReferenceId == referenceId)
            .Select(e => e.Key.Chunk)
            .ToList();

        // Fallback to chunking if no embeddings exist yet
        if (chunks.Count == 0)
            return _contentReferenceService.ChunkContent(item.RagText, maxTokens);

        return chunks;
    }

    /// <summary>
    /// Performs a semantic search for the most relevant content chunks in a document or section, given a query string.
    /// This uses precomputed embeddings for efficiency. Use this to find the most relevant passages for a specific question or topic.
    /// </summary>
    /// <param name="referenceId">The unique identifier of the content reference item.</param>
    /// <param name="query">The search query or question to match against the document's content.</param>
    /// <param name="topN">The number of top matching chunks to return (default: 5).</param>
    /// <param name="maxTokens">The maximum number of tokens per chunk (default: 1200). Should match the chunking used for embeddings.</param>
    /// <returns>A list of the most relevant content chunks, ordered by semantic similarity to the query.</returns>
    [KernelFunction, Description("Search for the most relevant content chunks in a ContentReferenceItem using a query string. This uses precomputed embeddings for efficiency. Use this to find the most relevant passages for a specific question or topic within a document or section.")]
    public async Task<List<string>> SearchSimilarChunks(
        [Description("The unique identifier of the content reference item.")] Guid referenceId,
        [Description("The search query or question to match against the document's content.")] string query,
        [Description("The number of top matching chunks to return (default: 5).")] int topN = 5,
        [Description("The maximum number of tokens per chunk (default: 1200). Should match the chunking used for embeddings.")] int maxTokens = 1200)
    {
        var items = await _contentReferenceService.GetContentReferenceItemsFromIdsAsync([referenceId]);
        var item = items.FirstOrDefault();
        if (item == null || string.IsNullOrEmpty(item.RagText)) return new List<string>();

        // Use cached or precomputed embeddings
        var embeddingsDict = await _contentReferenceService.GetOrCreateEmbeddingsForContentAsync([item], maxTokens);
        var chunkEmbeddings = embeddingsDict
            .Where(e => e.Key.ReferenceId == referenceId)
            .ToDictionary(e => e.Key.Chunk, e => e.Value);

        var queryEmbedding = await _contentReferenceService.GenerateEmbeddingsForQueryAsync(query); 
        var scores = _contentReferenceService.CalculateSimilarityScores(queryEmbedding, chunkEmbeddings);
        return _contentReferenceService.SelectTopChunks(scores, topN);
    }
}
