using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentCreatedInDatabase(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string FileHash { get; set; }
}
