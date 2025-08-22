using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models.SourceReferences;

/// <summary>
/// Aggregated vector store search result grouping multiple chunk partitions for a single document.
/// Replaces the previous private nested class inside SemanticKernelVectorStoreRepository.
/// </summary>
public sealed class VectorStoreAggregatedSourceReferenceItem : SourceReferenceItem
{
    public string IndexName { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public double Score { get; set; }

    /// <summary>
    /// Full chunk materialization used at runtime only (not persisted). UI should request lazily when needed.
    /// </summary>
    public List<DocumentChunk> Chunks { get; } = new();

    /// <summary>
    /// Persisted list of chunk identifiers (partition numbers) captured at creation time.
    /// Stored as a comma-separated list to allow lazy rehydration from the vector store without eager chunk payloads.
    /// </summary>
    public string? StoredPartitionNumbers { get; set; }

    public override string? SourceOutput { get; set; }

    public override void SetBasicParameters()
    {
        SourceReferenceType = SourceReferenceType.GeneralKnowledge;
        Description = "Document fragments from Semantic Kernel Vector Store";
        SourceReferenceLinkType = Enums.SourceReferenceLinkType.SystemNonProxiedUrl;
    }
}
