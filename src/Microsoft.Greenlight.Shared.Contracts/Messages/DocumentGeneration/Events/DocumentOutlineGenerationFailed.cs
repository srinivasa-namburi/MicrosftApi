using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;

/// <summary>
/// Event raised when a document outline generation fails.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record DocumentOutlineGenerationFailed(Guid CorrelationId) : CorrelatedBy<Guid>;
