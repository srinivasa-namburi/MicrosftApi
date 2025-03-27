using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationStepFailed(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ValidationPipelineExecutionStepId { get; init; }
        public string ErrorMessage { get; init; }
    }
}