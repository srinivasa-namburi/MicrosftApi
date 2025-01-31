using MassTransit;

namespace Microsoft.Greenlight.Shared.SagaState;

/// <summary>
/// Represents the state of the Review Execution Saga.
/// </summary>
public class ReviewExecutionSagaState : SagaStateMachineInstance
{
    /// <summary>
    /// Gets or sets the correlation ID.
    /// </summary>
    public Guid CorrelationId { get; set; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public string CurrentState { get; set; } = null!;

    /// <summary>
    /// Gets or sets the exported document link ID.
    /// </summary>
    public Guid ExportedDocumentLinkId { get; set; }

    /// <summary>
    /// Gets or sets the total number of questions.
    /// </summary>
    public int TotalNumberOfQuestions { get; set; }

    /// <summary>
    /// Gets or sets the number of questions answered.
    /// </summary>
    public int NumberOfQuestionsAnswered { get; set; }

    /// <summary>
    /// Gets or sets the number of questions answered with sentiment.
    /// </summary>
    public int NumberOfQuestionsAnsweredWithSentiment { get; set; }

    //public List<ReviewQuestionAnswer> ReviewQuestionsAnswered { get; set; } = [];

    //public List<ReviewQuestionAnswer> ReviewQuestionsAnsweredWithSentiment { get; set; } = [];
}
