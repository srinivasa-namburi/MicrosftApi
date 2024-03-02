using MassTransit;


namespace ProjectVico.V2.Shared.SagaState;

public class DocumentGenerationSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }
    public string? ReactorModel { get; set; }
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public DateOnly? ProjectedProjectStartDate { get; set; }
    public DateOnly? ProjectedProjectEndDate { get; set; }
    public int NumberOfContentNodesToGenerate { get; set; }
    public int NumberOfContentNodesGenerated { get; set; }

    public string CurrentState { get; set; }

}