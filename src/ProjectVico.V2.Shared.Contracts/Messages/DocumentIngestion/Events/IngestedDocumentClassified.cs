using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassified(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string ClassificationShortCode { get; set; }

}