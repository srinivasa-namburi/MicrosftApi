using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands
{
    public record StartValidationPipeline(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid GeneratedDocumentId { get; init; }
    }
}