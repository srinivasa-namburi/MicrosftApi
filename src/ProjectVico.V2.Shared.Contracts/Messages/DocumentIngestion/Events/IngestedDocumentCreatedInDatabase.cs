using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;

public record IngestedDocumentCreatedInDatabase(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string FileHash { get; set; }
}