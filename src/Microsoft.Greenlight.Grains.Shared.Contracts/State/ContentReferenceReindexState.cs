using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts.State;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ContentReferenceReindexState
{
    public string Id { get; set; } = string.Empty;
    public ContentReferenceType ReferenceType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Processed { get; set; }
    public int Failed { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool Running { get; set; }
    public List<string> Errors { get; set; } = new();

    /// <summary>
    /// Optional per-source progress details (e.g., by ContentReferenceSourceId).
    /// Only populated during active reindexing runs.
    /// </summary>
    public List<ContentReferenceReindexSourceProgress> Sources { get; set; } = new();
}

/// <summary>
/// Per-source progress model for content reference reindexing.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ContentReferenceReindexSourceProgress
{
    /// <summary>
    /// Source identifier (e.g., ContentReferenceSourceId) as string for display; may be null/empty.
    /// </summary>
    public string? SourceId { get; set; }

    public int Total { get; set; }
    public int Processed { get; set; }
    public int Failed { get; set; }
}
