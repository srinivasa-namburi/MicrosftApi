using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Tracks a content reference that has been materialized as a vector-store document (set of chunks)
/// in a specific Semantic Kernel index.
/// </summary>
public class ContentReferenceVectorDocument : EntityBase
{
    /// <summary>
    /// The backing content reference item ID.
    /// </summary>
    public Guid ContentReferenceItemId { get; set; }

    /// <summary>
    /// The content reference type (duplicated for convenience/queries).
    /// </summary>
    public ContentReferenceType ReferenceType { get; set; }

    /// <summary>
    /// The vector store index/collection name where this reference's chunks are stored.
    /// </summary>
    public required string VectorStoreIndexName { get; set; }

    /// <summary>
    /// Document identifier used in the vector store (stable key, e.g., cr-{referenceId}).
    /// </summary>
    public required string VectorStoreDocumentId { get; set; }

    /// <summary>
    /// Approximate number of chunks written.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Timestamp when last indexed.
    /// </summary>
    public DateTime? IndexedUtc { get; set; }

    /// <summary>
    /// Flag indicating whether vector indexing succeeded for the current content state.
    /// </summary>
    public bool IsIndexed { get; set; }
}

