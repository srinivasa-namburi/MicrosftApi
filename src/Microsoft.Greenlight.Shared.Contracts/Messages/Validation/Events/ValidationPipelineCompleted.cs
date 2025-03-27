using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationPipelineCompleted(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ValidationPipelineExecutionId { get; init; }
    }
}