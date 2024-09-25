using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Events;

public record ReviewDocumentIngested(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required Guid ExportedDocumentLinkId { get; init; }
    public required int TotalNumberOfQuestions { get; init; }

}