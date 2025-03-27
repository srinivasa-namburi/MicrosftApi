using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.SagaState;
using Microsoft.Greenlight.Worker.Validation.Sagas.Activities;

namespace Microsoft.Greenlight.Worker.Validation.Sagas
{
    public class ValidationPipelineSaga : MassTransitStateMachine<ValidationPipelineSagaState>
    {
        private readonly ILogger<ValidationPipelineSaga> _logger;

        public ValidationPipelineSaga(ILogger<ValidationPipelineSaga> logger)
        {
            _logger = logger;

            InstanceState(x => x.CurrentState);

            // Events
            Event(() => StartValidationPipelineRequested, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => ValidationStepsLoaded, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => ValidationStepCompleted, x => x.CorrelateById(m => m.Message.CorrelationId));
            Event(() => ValidationStepFailed, x => x.CorrelateById(m => m.Message.CorrelationId));
            
            Initially(
                When(StartValidationPipelineRequested)
                    .Then(context =>
                    {
                        _logger.LogInformation("Starting pipeline {PipelineId} for document {DocumentId}",
                            context.Message.CorrelationId, context.Message.GeneratedDocumentId);
                        context.Saga.CorrelationId = context.Message.CorrelationId;
                        context.Saga.GeneratedDocumentId = context.Message.GeneratedDocumentId;
                        context.Saga.CurrentStepIndex = -1;
                    })
                    .Activity(x => x.OfType<LoadValidationStepsActivity>())
                    .TransitionTo(LoadingSteps)
            );

            During(LoadingSteps,
                When(ValidationStepsLoaded)
                    .Then(context =>
                    {
                        _logger.LogInformation("Loaded {Count} steps for pipeline {PipelineId}",
                            context.Message.OrderedSteps.Count, context.Saga.CorrelationId);
                        context.Saga.OrderedSteps = context.Message.OrderedSteps;
                        context.Saga.CurrentStepIndex = 0;
                    })
                    .Activity(x => x.OfType<ExecuteCurrentStepAfterLoadingActivity>())
                    .TransitionTo(ExecutingStep)
            );

            During(ExecutingStep,
                When(ValidationStepCompleted)
                    .Then(context =>
                    {
                        _logger.LogInformation("Step completed: {StepId}", context.Message.ValidationPipelineExecutionStepId);
                        context.Saga.CurrentStepIndex++;
                    })
                    .IfElse(context => context.Saga.CurrentStepIndex < context.Saga.OrderedSteps.Count,
                        thenClause => thenClause
                            // If there are more steps, execute the next one
                            .Activity(x => x.OfType<ExecuteNextStepActivity>())
                            .TransitionTo(ExecutingStep),
                                 elseClause => elseClause
                            .Activity(x => x.OfType<FinalizeValidationPipelineActivity>())
                            .Finalize()
                    )
            );

            During(ExecutingStep,
                When(ValidationStepFailed)
                    .Then(context =>
                    {
                        _logger.LogError("Step failed: {StepId} - {ErrorMessage}",
                            context.Message.ValidationPipelineExecutionStepId, context.Message.ErrorMessage);
                    })
                    .Activity(x => x.OfType<HandleFailedValidationActivity>())
                    .TransitionTo(Failed)
            );

            SetCompletedWhenFinalized();
        }

        // States
        public State LoadingSteps { get; private set; }
        public State ExecutingStep { get; private set; }
        public State Failed { get; private set; }

        // Events
        public Event<StartValidationPipeline> StartValidationPipelineRequested { get; private set; }
        public Event<ValidationStepsLoaded> ValidationStepsLoaded { get; private set; }
        public Event<ValidationStepCompleted> ValidationStepCompleted { get; private set; }
        public Event<ValidationStepFailed> ValidationStepFailed { get; private set; }
    }
}
