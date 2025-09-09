namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Represents the various states of ingestion process of documents.
/// </summary>
public enum IngestionState
{
    /// <summary>
    /// The file has been discovered for ingestion.
    /// </summary>
    Discovered = 50,

    /// <summary>
    /// The file has been uploaded.
    /// </summary>
    Uploaded = 100,

    /// <summary>
    /// The file is being copied from the ingestion drop point.
    /// </summary>
    FileCopying = 150,

    /// <summary>
    /// The file has been copied to its final location.
    /// </summary>
    FileCopied = 200,

    /// <summary>
    /// The file was already acknowledged at the source and discovered for a new consumer (DL/DP).
    /// Use this to flow through the copy stage without performing any physical copy, while still
    /// honoring the ingestion concurrency and queueing semantics.
    /// </summary>
    DiscoveredForConsumer = 250,

    /// <summary>
    /// The file is being processed (e.g., indexed, classified).
    /// </summary>
    Processing = 300,

    /// <summary>
    /// The ingestion process is complete.
    /// </summary>
    Complete = 800,

    /// <summary>
    /// The ingestion process has failed.
    /// </summary>
    Failed = 900,

    /// <summary>
    /// The file is marked as deleted and can be reuploaded regardless of hash.
    /// </summary>
    Deleted = 950
}
