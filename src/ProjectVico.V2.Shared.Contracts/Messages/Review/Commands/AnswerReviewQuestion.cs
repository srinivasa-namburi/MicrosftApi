using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.Review.Commands;

public record AnswerReviewQuestion(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required ReviewQuestionInfo ReviewQuestion { get; init; }
    public int? QuestionNumber { get; set; }
    public int? TotalQuestions { get; set; }
}