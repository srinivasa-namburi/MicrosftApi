using MassTransit;
namespace Microsoft.Greenlight.Shared.SagaState;

/// <summary>
/// Represents the state of the document generation saga.
/// </summary>
public class DocumentGenerationSagaState : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public string CurrentState { get; set; } = null!;

    /// <summary>
    /// Gets or sets the document title.
    /// </summary>
    public string? DocumentTitle { get; set; }

    /// <summary>
    /// Gets or sets the author OID.
    /// </summary>
    public string? AuthorOid { get; set; }

    /// <summary>
    /// Gets or sets the metadata JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the number of content nodes to generate.
    /// </summary>
    public int NumberOfContentNodesToGenerate { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of content nodes generated.
    /// </summary>
    public int NumberOfContentNodesGenerated { get; set; } = 0;

    /// <summary>
    /// Gets or sets the document process name.
    /// </summary>
    public string? DocumentProcessName { get; set; } = "US.NuclearLicensing";

    /// <summary>
    /// Gets or sets the metadata ID.
    /// </summary>
    public Guid? MetadataId { get; set; }

    //public DocumentGenerationRequest DocumentGenerationRequest { get; set; } = new DocumentGenerationRequest();
}
