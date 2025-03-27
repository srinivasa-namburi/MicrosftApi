using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands
{
    public record RetryValidationStep (Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ValidationPipelineExecutionStepId { get; init; }
    }
}