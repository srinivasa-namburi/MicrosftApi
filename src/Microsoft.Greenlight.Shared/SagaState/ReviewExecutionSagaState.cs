using MassTransit;
using Microsoft.Greenlight.Shared.Models.Review;

namespace Microsoft.Greenlight.Shared.SagaState;

public class ReviewExecutionSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }
    public Guid ExportedDocumentLinkId { get; set; }

    public int TotalNumberOfQuestions { get; set; }
    public int NumberOfQuestionsAnswered { get; set; }
    public int NumberOfQuestionsAnsweredWithSentiment { get; set; }

    //public List<ReviewQuestionAnswer> ReviewQuestionsAnswered { get; set; } = [];

    //public List<ReviewQuestionAnswer> ReviewQuestionsAnsweredWithSentiment { get; set; } = [];
}
