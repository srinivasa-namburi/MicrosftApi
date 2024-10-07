using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Review.Events;
using Microsoft.Greenlight.Shared.SagaState;
// ReSharper disable MemberCanBePrivate.Global
// State variables must be public for MassTransit to work

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Sagas;

public class ReviewExecutionSaga : MassTransitStateMachine<ReviewExecutionSagaState>
{
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



    public State Ingesting { get; set; } = null!;
    public State Answering { get; set; } = null!;
    public Event<ExecuteReviewRequest> ReviewExecutionRequested { get; private set; }
    public Event<ReviewDocumentIngested> ReviewDocumentIngested { get; private set; }
    public Event<ReviewQuestionAnswered> ReviewQuestionAnswered { get; private set; }
    public Event<ReviewQuestionAnswerAnalyzed> ReviewQuestionAnswerAnalyzed { get; private set; }
}
