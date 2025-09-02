using Microsoft.Greenlight.Grains.Document.Contracts.Models;
using Orleans;


[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class DocumentGenerationState
{
    public Guid Id { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string? AuthorOid { get; set; }
    public string? DocumentProcessName { get; set; }
    public string? MetadataJson { get; set; }
    public Guid? MetadataId { get; set; }
    public DocumentGenerationStatus Status { get; set; } = DocumentGenerationStatus.Pending;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public int NumberOfContentNodesToGenerate { get; set; }
    public int NumberOfContentNodesGenerated { get; set; }
    public string? FailureReason { get; set; }
    public string? FailureDetails { get; set; }
    public Guid CorrelationId { get; set; }
    /// <summary>
    /// The Provider Subject ID (OID/sub) of the user who started this orchestration.
    /// </summary>
    public string? StartedByProviderSubjectId { get; set; }
}