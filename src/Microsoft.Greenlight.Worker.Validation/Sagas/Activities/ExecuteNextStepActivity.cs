using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.SagaState;


namespace Microsoft.Greenlight.Worker.Validation.Sagas.Activities;

public class ExecuteNextStepActivity : IStateMachineActivity<ValidationPipelineSagaState, ValidationStepCompleted>
{
    private readonly ISendEndpointProvider _sendEndpointProvider;
    private readonly ILogger<ExecuteNextStepActivity> _logger;

    public ExecuteNextStepActivity(ISendEndpointProvider sendEndpointProvider, ILogger<ExecuteNextStepActivity> logger)
    {
        _sendEndpointProvider = sendEndpointProvider;
        _logger = logger;
    }

    public async Task Execute(BehaviorContext<ValidationPipelineSagaState, ValidationStepCompleted> context, IBehavior<ValidationPipelineSagaState, ValidationStepCompleted> next)
    {
        var saga = context.Saga;
        var stepInfo = saga.OrderedSteps[saga.CurrentStepIndex];
        _logger.LogInformation("Executing next step {StepId} (order {Order})", stepInfo.StepId, stepInfo.Order);

        
        await context.Publish(new ExecuteValidationStep(saga.CorrelationId)
        {
            ValidationPipelineExecutionStepId = stepInfo.StepId,
            ExecutionType = stepInfo.ExecutionType
        });

        await next.Execute(context);
    }

    public async Task Faulted<TException>(BehaviorExceptionContext<ValidationPipelineSagaState, ValidationStepCompleted, TException> context, IBehavior<ValidationPipelineSagaState, ValidationStepCompleted> next) where TException : Exception
    {
        _logger.LogError(context.Exception, "Error executing step");
        await next.Faulted(context);
    }

    public void Probe(ProbeContext context)
    {
        context.CreateScope("execute-next-step");
    }

    public void Accept(StateMachineVisitor visitor)
    {
        visitor.Visit(this);
    }
}