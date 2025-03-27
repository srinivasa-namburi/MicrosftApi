using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events
{
    public record ValidationPipelineFailed(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public string ErrorMessage { get; init; }
    }
}