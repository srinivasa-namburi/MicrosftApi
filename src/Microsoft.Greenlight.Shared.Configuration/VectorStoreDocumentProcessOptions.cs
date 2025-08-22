// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Configuration;

/// <summary>
/// Vector store configuration options that can be customized per document process.
/// </summary>
public class VectorStoreDocumentProcessOptions
{
    /// <summary>
    /// Chunk size in tokens. If null, uses the global VectorStoreOptions.ChunkSize.
    /// </summary>
    public int? ChunkSize { get; set; }

    /// <summary>
    /// Chunk overlap in tokens. If null, uses the global VectorStoreOptions.ChunkOverlap.
    /// </summary>
    public int? ChunkOverlap { get; set; }

    /// <summary>
    /// Minimum relevance score for search results. If null, uses the global VectorStoreOptions.MinRelevanceScore.
    /// </summary>
    public double? MinRelevanceScore { get; set; }

    /// <summary>
    /// Maximum number of search results. If null, uses the global VectorStoreOptions.MaxSearchResults.
    /// </summary>
    public int? MaxSearchResults { get; set; }

    /// <summary>
    /// Chunking mode (null => Simple by default).
    /// </summary>
    public Microsoft.Greenlight.Shared.Enums.TextChunkingMode? ChunkingMode { get; set; }

    /// <summary>
    /// Creates options from DocumentProcessInfo, falling back to global options where needed.
    /// </summary>
    /// <param name="documentProcess">Document process information.</param>
    /// <param name="globalOptions">Global vector store options.</param>
    /// <returns>Effective options for the document process.</returns>
    [Obsolete("Prefer backend-model based factory methods in VectorStoreDocumentProcessOptionsExtensions when available.")]
    public static VectorStoreDocumentProcessOptions FromDocumentProcess(
        DocumentProcessInfo? documentProcess,
        VectorStoreOptions globalOptions)
    {
        return new VectorStoreDocumentProcessOptions
        {
            ChunkSize = documentProcess?.VectorStoreChunkSize ?? globalOptions.ChunkSize,
            ChunkOverlap = documentProcess?.VectorStoreChunkOverlap ?? globalOptions.ChunkOverlap,
            MinRelevanceScore = documentProcess?.MinimumRelevanceForCitations ?? globalOptions.MinRelevanceScore,
            MaxSearchResults = documentProcess?.NumberOfCitationsToGetFromRepository ?? globalOptions.MaxSearchResults,
            ChunkingMode = documentProcess?.VectorStoreChunkingMode
        };
    }

    /// <summary>
    /// Gets the effective chunk size, ensuring it's always positive.
    /// </summary>
    public int GetEffectiveChunkSize() => Math.Max(ChunkSize ?? 1000, 100);

    /// <summary>
    /// Gets the effective chunk overlap, ensuring it's not negative and less than chunk size.
    /// </summary>
    public int GetEffectiveChunkOverlap()
    {
        var chunkSize = GetEffectiveChunkSize();
        var overlap = Math.Max(ChunkOverlap ?? 100, 0);
        return Math.Min(overlap, chunkSize / 2); // Overlap should not exceed half the chunk size
    }

    /// <summary>
    /// Gets the effective minimum relevance score, ensuring it's between 0 and 1.
    /// </summary>
    public double GetEffectiveMinRelevanceScore() => Math.Clamp(MinRelevanceScore ?? 0.7, 0.0, 1.0);

    /// <summary>
    /// Gets the effective maximum search results, ensuring it's positive.
    /// </summary>
    public int GetEffectiveMaxSearchResults() => Math.Max(MaxSearchResults ?? 10, 1);

    /// <summary>
    /// Gets the effective chunking mode (defaults to Simple).
    /// </summary>
    public Microsoft.Greenlight.Shared.Enums.TextChunkingMode GetEffectiveChunkingMode() => ChunkingMode ?? Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Simple;
}
