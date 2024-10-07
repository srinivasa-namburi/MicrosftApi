using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

public record GeneratedDocumentCreated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid MetaDataId { get; set; }
};
