using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.SagaState;

public class DocumentGenerationSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }

    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }
    
    //public DocumentGenerationRequest DocumentGenerationRequest { get; set; } = new DocumentGenerationRequest();
    public string? MetadataJson { get; set; }
    
    public int NumberOfContentNodesToGenerate { get; set; } = 0;
    public int NumberOfContentNodesGenerated { get; set; } = 0;
    public string? DocumentProcessName { get; set; } = "US.NuclearLicensing";
    public Guid? MetadataId { get; set; }
}
