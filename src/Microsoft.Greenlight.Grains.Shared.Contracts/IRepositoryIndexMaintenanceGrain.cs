using Orleans;

public interface IRepositoryIndexMaintenanceGrain : IGrainWithGuidKey
{
    Task ExecuteAsync();
    
    /// <summary>
    /// Validates all vector store schemas and triggers reindexing for incompatible indexes
    /// </summary>
    Task ValidateAndReindexSchemasAsync();
    
    /// <summary>
    /// Gets the status of all vector store indexes and any ongoing reindexing operations
    /// </summary>
    Task<IndexStatusSummary> GetIndexStatusSummaryAsync();
}

/// <summary>
/// Summary of all vector store index statuses
/// </summary>
public class IndexStatusSummary
{
    public required List<IndexStatus> Indexes { get; init; }
    public int TotalIndexes => Indexes.Count;
    public int HealthyIndexes => Indexes.Count(i => i.Status == IndexHealthStatus.Healthy);
    public int ReindexingIndexes => Indexes.Count(i => i.Status == IndexHealthStatus.Reindexing);
    public int UnhealthyIndexes => Indexes.Count(i => i.Status == IndexHealthStatus.SchemaIncompatible);
}

/// <summary>
/// Status of a single vector store index
/// </summary>
public class IndexStatus
{
    public required string IndexName { get; set; }
    public required string DocumentLibraryOrProcessName { get; set; }
    public IndexHealthStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public DateTime LastCheckedUtc { get; set; }
    public int? EmbeddingDimensions { get; set; }
    public bool HasDocumentReferenceField { get; set; }
    public string? ReindexingRunId { get; set; }
}

/// <summary>
/// Health status of a vector store index
/// </summary>
public enum IndexHealthStatus
{
    /// <summary>Index exists and schema is compatible</summary>
    Healthy,
    /// <summary>Index schema is incompatible and needs reindexing</summary>
    SchemaIncompatible,
    /// <summary>Index is currently being reindexed</summary>
    Reindexing,
    /// <summary>Index doesn't exist</summary>
    Missing,
    /// <summary>Unable to determine index status</summary>
    Unknown
}