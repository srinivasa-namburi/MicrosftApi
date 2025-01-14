using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

/// <summary>
/// Event raised when a document is ingested for review.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the event.</param>
public record ReviewDocumentIngested(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// The ID of the exported document link.
    /// </summary>
    public required Guid ExportedDocumentLinkId { get; init; }
    /// <summary>
    /// The total number of questions in the document.
    /// </summary>
    public required int TotalNumberOfQuestions { get; init; }

}
