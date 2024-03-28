using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record ReportContentGenerationSubmitted(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public int NumberOfContentNodesToGenerate { get; set; }
    public string AuthorOid { get; set; }
}