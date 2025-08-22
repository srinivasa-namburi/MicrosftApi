using System.ComponentModel;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Document;

/// <summary>
/// Represents an aggregated search result coming from the Semantic Kernel Vector Store.
/// Mirrors the runtime model VectorStoreAggregatedSourceReferenceItem but trimmed for contract usage.
/// </summary>
public class VectorStoreSourceReferenceItemInfo : SourceReferenceItemInfo
{
    /// <summary>
    /// Index (collection) name from which the chunks were retrieved.
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Document identifier grouping the returned chunks.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Original file name.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Highest similarity score across the grouped chunks.
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Individual retrieved chunks (partitions).
    /// </summary>
    public List<VectorStoreDocumentChunkInfo> Chunks { get; set; } = new();

    /// <summary>
    /// Comma-separated list of partition numbers captured at creation time for lazy rehydration.
    /// </summary>
    public string? StoredPartitionNumbers { get; set; }
}

/// <summary>
/// DTO representing a single retrieved chunk from the vector store.
/// </summary>
public class VectorStoreDocumentChunkInfo
{
    public string Text { get; set; } = string.Empty;
    public double Relevance { get; set; }
    public int PartitionNumber { get; set; }
    public int SizeInBytes { get; set; }
    public Dictionary<string, List<string?>> Tags { get; set; } = new();
    [Description("Last update timestamp for the underlying stored chunk record.")]
    public DateTimeOffset LastUpdate { get; set; }
}
