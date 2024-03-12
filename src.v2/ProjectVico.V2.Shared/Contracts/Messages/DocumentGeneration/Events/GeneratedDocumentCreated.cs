using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record GeneratedDocumentCreated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid MetaDataId { get; set; }
};