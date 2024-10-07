using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassified(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string ClassificationShortCode { get; set; }

}
