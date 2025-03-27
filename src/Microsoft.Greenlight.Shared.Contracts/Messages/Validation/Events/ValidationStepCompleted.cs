using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationStepCompleted(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ValidationPipelineExecutionStepId { get; init; }
    }
}