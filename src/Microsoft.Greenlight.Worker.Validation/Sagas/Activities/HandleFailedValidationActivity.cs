using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.Validation.Sagas.Activities
{
    // For ValidationStepFailed event
    public class HandleFailedValidationActivity : IStateMachineActivity<ValidationPipelineSagaState, ValidationStepFailed>
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<HandleFailedValidationActivity> _logger;

        public HandleFailedValidationActivity(IPublishEndpoint publishEndpoint, ILogger<HandleFailedValidationActivity> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        public async Task Execute(BehaviorContext<ValidationPipelineSagaState, ValidationStepFailed> context, IBehavior<ValidationPipelineSagaState, ValidationStepFailed> next)
        {
            _logger.LogError("Marking pipeline {PipelineId} as failed due to step failure", context.Saga.CorrelationId);

            await _publishEndpoint.Publish(new ValidationPipelineFailed(context.Saga.CorrelationId)
            {
                ErrorMessage = $"Step failed: {context.Message.ErrorMessage}"
            });

            await next.Execute(context);
        }

        public async Task Faulted<TException>(BehaviorExceptionContext<ValidationPipelineSagaState, ValidationStepFailed, TException> context, IBehavior<ValidationPipelineSagaState, ValidationStepFailed> next) where TException : Exception
        {
            _logger.LogError(context.Exception, "Error handling validation failure");
            await next.Faulted(context);
        }

        public void Probe(ProbeContext context)
        {
            context.CreateScope("handle-failed-validation");
        }

        public void Accept(StateMachineVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}