using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.Validation.Sagas.Activities;

public class FinalizeValidationPipelineActivity : IStateMachineActivity<ValidationPipelineSagaState, ValidationStepCompleted>
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<FinalizeValidationPipelineActivity> _logger;

    public FinalizeValidationPipelineActivity(IPublishEndpoint publishEndpoint, ILogger<FinalizeValidationPipelineActivity> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Execute(BehaviorContext<ValidationPipelineSagaState, ValidationStepCompleted> context, IBehavior<ValidationPipelineSagaState, ValidationStepCompleted> next)
    {
        _logger.LogInformation("Validation pipeline {PipelineId} completed", context.Saga.CorrelationId);
        
        await _publishEndpoint.Publish(new ValidationPipelineCompleted(context.Saga.CorrelationId)
        {
            ValidationPipelineExecutionId = context.Saga.CorrelationId
        });

        await next.Execute(context);
    }

    public async Task Faulted<TException>(BehaviorExceptionContext<ValidationPipelineSagaState, ValidationStepCompleted, TException> context, IBehavior<ValidationPipelineSagaState, ValidationStepCompleted> next) where TException : Exception
    {
        _logger.LogError(context.Exception, "Error finalizing pipeline");
        await next.Faulted(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateScope("finalize-pipeline");
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }
}