using MassTransit;
using ProjectVico.V2.Shared.Classification.Models;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentClassified(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public DocumentClassificationType ClassificationType { get; set; }
    public string ClassificationShortCode { get; set; }

}