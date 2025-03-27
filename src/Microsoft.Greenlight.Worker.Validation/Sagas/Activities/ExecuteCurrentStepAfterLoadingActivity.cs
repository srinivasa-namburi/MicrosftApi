using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.SagaState;


namespace Microsoft.Greenlight.Worker.Validation.Sagas.Activities;
public class ExecuteCurrentStepAfterLoadingActivity : IStateMachineActivity<ValidationPipelineSagaState, ValidationStepsLoaded>
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<ExecuteCurrentStepAfterLoadingActivity> _logger;

    public ExecuteCurrentStepAfterLoadingActivity(ISendEndpointProvider sendEndpointProvider, ILogger<ExecuteCurrentStepAfterLoadingActivity> logger)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
    }

    public async Task Execute(BehaviorContext<ValidationPipelineSagaState, ValidationStepsLoaded> context, IBehavior<ValidationPipelineSagaState, ValidationStepsLoaded> next)
    {
        var saga = context.Saga;
        var stepInfo = saga.OrderedSteps[saga.CurrentStepIndex];
        _logger.LogInformation("Executing step {StepId} (order {Order})", stepInfo.StepId, stepInfo.Order);

        // Send command

        await context.Publish(new ExecuteValidationStep(saga.CorrelationId)
        {
            ValidationPipelineExecutionStepId = stepInfo.StepId,
            ExecutionType = stepInfo.ExecutionType
        });

        await next.Execute(context);
    }

    public async Task Faulted<TException>(BehaviorExceptionContext<ValidationPipelineSagaState, ValidationStepsLoaded, TException> context, IBehavior<ValidationPipelineSagaState, ValidationStepsLoaded> next) where TException : Exception
    {
        _logger.LogError(context.Exception, "Error executing step");
        await next.Faulted(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateScope("execute-current-step-after-loading");
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }
}