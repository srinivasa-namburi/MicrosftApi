using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;

public record AnswerReviewQuestion(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required ReviewQuestionInfo ReviewQuestion { get; init; }
    public int? QuestionNumber { get; set; }
    public int? TotalQuestions { get; set; }
}
