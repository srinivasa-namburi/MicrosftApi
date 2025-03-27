using MassTransit;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands
{
    public record ExecuteValidationStep(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public required Guid ValidationPipelineExecutionStepId { get; set; }
        public required ValidationPipelineExecutionType ExecutionType { get; init; }

    }
}