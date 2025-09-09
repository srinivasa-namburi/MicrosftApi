// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Configuration;

/// <summary>
/// Configuration options for Semantic Kernel Vector Store implementations.
/// </summary>
public class VectorStoreOptions
{
    /// <summary>
    /// The type of vector store to use (PostgreSQL, Azure AI Search, etc.).
    /// </summary>
    public VectorStoreType StoreType { get; set; } = VectorStoreType.AzureAISearch;

    /// <summary>
    /// Default chunk size for document processing.
    /// </summary>
    public int ChunkSize { get; set; } = 1000;

    /// <summary>
    /// Default chunk overlap for document processing.
    /// </summary>
    public int ChunkOverlap { get; set; } = 200;

    /// <summary>
    /// The dimension of the vectors stored in the collection. Defaults to 1536 for text-embedding-ada-002.
    /// </summary>
    public int VectorSize { get; set; } = 1536;

    /// <summary>
    /// Maximum number of search results to return.
    /// </summary>
    public int MaxSearchResults { get; set; } = 10;

    /// <summary>
    /// Minimum relevance score for search results.
    /// </summary>
    public double MinRelevanceScore { get; set; } = 0.7;


    /// <summary>
    /// Time-to-live (minutes) for Redis document chunk cache entries (Semantic Kernel provider). Default 30.
    /// </summary>
    public int DocumentChunkCacheTtlMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to maintain a secondary Redis index mapping file names to document IDs to enable targeted cache invalidation on delete.
    /// </summary>
    public bool EnableFileNameDocIdCacheIndex { get; set; } = true;

    /// <summary>
    /// Optional minimum similarity score cutoff; results below (after normalization) are discarded once at least <see cref="MinResultsBeforeCutoff"/> are gathered.
    /// </summary>
    public double? ScoreCutoff { get; set; } = null;

    /// <summary>
    /// Minimum number of results to collect before applying <see cref="ScoreCutoff"/> early-exit logic.
    /// </summary>
    public int MinResultsBeforeCutoff { get; set; } = 3;

    /// <summary>
    /// Enables hybrid (semantic + keyword/BM25) scoring when backend supports it.
    /// </summary>
    public bool EnableHybridScoring { get; set; } = false;

    /// <summary>
    /// Blend ratio (0..1) where 1 = pure vector score, 0 = pure keyword score.
    /// </summary>
    public double HybridVectorWeight { get; set; } = 0.7;

    /// <summary>
    /// Maximum allowed chunk text length (characters); longer chunks truncated with ellipsis.
    /// </summary>
    public int MaxChunkTextLength { get; set; } = 8000;

    /// <summary>
    /// Enable background warmup (e.g., pre-create collections, JIT compile expressions) during app startup.
    /// </summary>
    public bool EnableWarmup { get; set; } = true;

    /// <summary>
    /// When true, verify embedding vector dimension matches stored configuration; mismatches flagged.
    /// </summary>
    public bool EnableEmbeddingDimensionVerification { get; set; } = true;

    /// <summary>
    /// Per-ContentReferenceType embedding configuration.
    /// </summary>
    public ContentReferenceVectorOptions ContentReferences { get; set; } = new ContentReferenceVectorOptions();
}

/// <summary>
/// Embedding configuration for Content Reference types.
/// </summary>
public class ContentReferenceVectorOptions
{
    public ContentReferenceTypeOptions GeneratedDocument { get; set; } = new ContentReferenceTypeOptions();
    public ContentReferenceTypeOptions GeneratedSection { get; set; } = new ContentReferenceTypeOptions();
    public ContentReferenceTypeOptions ExternalFile { get; set; } = new ContentReferenceTypeOptions();
    public ContentReferenceTypeOptions ReviewItem { get; set; } = new ContentReferenceTypeOptions();
    public ContentReferenceTypeOptions ExternalLinkAsset { get; set; } = new ContentReferenceTypeOptions();
}

/// <summary>
/// Embedding options for a specific content reference type.
/// </summary>
public class ContentReferenceTypeOptions
{
    /// <summary>
    /// Optional specific embedding model deployment to use.
    /// If null, falls back to global embedding deployment.
    /// </summary>
    public Guid? EmbeddingModelDeploymentId { get; set; }

    /// <summary>
    /// Optional deployment name to use for embeddings (preferred over ID).
    /// If set, this value is used to resolve the deployment; ID is only used as a fallback for backward compatibility.
    /// </summary>
    public string? EmbeddingModelDeploymentName { get; set; }

    /// <summary>
    /// Optional dimensions override for the embedding model.
    /// If null, falls back to global VectorSize.
    /// </summary>
    public int? EmbeddingDimensionsOverride { get; set; }
}

