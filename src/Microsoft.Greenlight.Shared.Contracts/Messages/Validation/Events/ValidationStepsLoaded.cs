using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationStepsLoaded(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public List<ValidationPipelineSagaStepInfo> OrderedSteps { get; init; }
    }
}