using MassTransit;
using ProjectVico.V2.Shared.Models.Classification;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassified(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string ClassificationShortCode { get; set; }

}