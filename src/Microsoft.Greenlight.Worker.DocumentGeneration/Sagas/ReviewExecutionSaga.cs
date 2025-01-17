using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.SagaState;
// ReSharper disable MemberCanBePrivate.Global
// State variables must be public for MassTransit to work

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Sagas;

/// <summary>
/// The ReviewExecutionSaga class is the Mass Transit state machine responsible for coordinating the execution of a
/// review of a document.
/// </summary>
public class ReviewExecutionSaga : MassTransitStateMachine<ReviewExecutionSagaState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReviewExecutionSaga"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constructor is responsible for creating the Mass Transit messaging flow for reviewing a document.
    /// </para>
    /// </remarks>
    public ReviewExecutionSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => ReviewExecutionRequested,
            x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(ReviewExecutionRequested)
                .Then(context => { context.Saga.CorrelationId = context.Message.CorrelationId; })
                .Publish(context => new IngestReviewDocument(context.Saga.CorrelationId))
                .TransitionTo(Ingesting));

        During(Ingesting,
            When(ReviewDocumentIngested)
                .Then(context =>
                {
                    context.Saga.ExportedDocumentLinkId = context.Message.ExportedDocumentLinkId;
                    context.Saga.TotalNumberOfQuestions = context.Message.TotalNumberOfQuestions;
                })
                .Publish(context => new DistributeReviewQuestions(context.Saga.CorrelationId))
                .TransitionTo(Answering));

        During(Answering,
            When(ReviewQuestionAnswered)
                .Then(context => { context.Saga.NumberOfQuestionsAnswered++; })
                .Publish(context => new AnalyzeReviewQuestionAnswerSentiment(context.Saga.CorrelationId)
                {
                    ReviewQuestionAnswer = context.Message.Answer
                }),

            When(ReviewQuestionAnswerAnalyzed)
                .Then(context => { context.Saga.NumberOfQuestionsAnsweredWithSentiment++; })
                .IfElse(
                    context => context.Saga.NumberOfQuestionsAnsweredWithSentiment ==
                               context.Saga.TotalNumberOfQuestions,
                    x => x
                        .Publish(context => new ReviewQuestionAnsweredNotification(context.Message.CorrelationId)
                        {
                            ReviewQuestionAnswerId = context.Message.ReviewQuestionAnswerId
                        })
                        .Publish(context => new BackendProcessingMessageGenerated(
                            context.Message.CorrelationId,
                            "SYSTEM:ReviewInstanceCompleted"
                        ))
                        .Finalize(),
                    y => y
                        .Publish(context => new ReviewQuestionAnsweredNotification(context.Message.CorrelationId)
                        {
                            ReviewQuestionAnswerId = context.Message.ReviewQuestionAnswerId
                        })
                ));
    }

    /// <summary>
    /// Gets or sets the Ingesting <see cref="State"/> and its associated events.
    /// </summary>
    public State Ingesting { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Answering <see cref="State"/> and its associated events.
    /// </summary>
    public State Answering { get; set; } = null!;

    /// <summary>
    /// Gets the review requested <see cref="Event"/>.
    /// </summary>
    public Event<ExecuteReviewRequest> ReviewExecutionRequested { get; private set; } = null!;

    /// <summary>
    /// Gets the document to review ingested <see cref="Event"/>.
    /// </summary>
    public Event<ReviewDocumentIngested> ReviewDocumentIngested { get; private set; } = null!;

    /// <summary>
    /// Gets the review question answered <see cref="Event"/>.
    /// </summary>
    public Event<ReviewQuestionAnswered> ReviewQuestionAnswered { get; private set; } = null!;

    /// <summary>
    /// Gets the review question analyzed <see cref="Event"/>.
    /// </summary>
    public Event<ReviewQuestionAnswerAnalyzed> ReviewQuestionAnswerAnalyzed { get; private set; } = null!;
}
