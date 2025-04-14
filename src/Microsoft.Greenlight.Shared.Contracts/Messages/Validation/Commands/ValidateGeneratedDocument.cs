using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands
{
    /// <summary>
    /// Command to validate report content after generation has finished.
    /// </summary>
    /// <param name="CorrelationId">The correlation ID, which in this case is the GeneratedDocument ID.</param>
    public record ValidateGeneratedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
    {

    }
}