using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;

public record ReviewDocumentIngested(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ExportedDocumentLinkId { get; init; }
    public required int TotalNumberOfQuestions { get; init; }

}
