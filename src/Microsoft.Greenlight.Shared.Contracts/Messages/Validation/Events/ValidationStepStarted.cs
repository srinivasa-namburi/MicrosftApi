using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationStepStarted(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ValidationPipelineExecutionStepId { get; init; }
        public int StepIndex { get; init; }
    }
}