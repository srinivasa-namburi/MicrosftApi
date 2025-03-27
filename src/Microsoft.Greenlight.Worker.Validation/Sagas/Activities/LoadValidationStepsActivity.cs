using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.Validation.Sagas.Activities
{
    // Updated implementation using IStateMachineActivity<TSaga, TMessage>
    public class LoadValidationStepsActivity : IStateMachineActivity<ValidationPipelineSagaState, StartValidationPipeline>
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<LoadValidationStepsActivity> _logger;

        public LoadValidationStepsActivity(DocGenerationDbContext dbContext, IPublishEndpoint publishEndpoint, ILogger<LoadValidationStepsActivity> logger)
        {
            _dbContext = dbContext;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        // The new Execute method
        public async Task Execute(BehaviorContext<ValidationPipelineSagaState, StartValidationPipeline> context, IBehavior<ValidationPipelineSagaState, StartValidationPipeline> next)
        {
            var executionId = context.Message.CorrelationId;

            var execution = await _dbContext.ValidationPipelineExecutions
                .Include(x => x.ExecutionSteps)
                .FirstOrDefaultAsync(x => x.Id == executionId);

            if (execution == null)
            {
                _logger.LogError("Execution {ExecutionId} not found", executionId);
                await _publishEndpoint.Publish(new ValidationPipelineFailed(context.Message.CorrelationId)
                {
                    ErrorMessage = "Execution not found"
                });
                // Skip calling next since we have an error condition
                return;
            }

            var orderedSteps = execution.ExecutionSteps
                .OrderBy(x => x.Order)
                .Select(x => new ValidationPipelineSagaStepInfo
                {
                    StepId = x.Id,
                    Order = x.Order,
                    ExecutionType = x.PipelineExecutionType
                })
                .ToList();

            await _publishEndpoint.Publish(new ValidationStepsLoaded(context.Message.CorrelationId)
            {
                OrderedSteps = orderedSteps
            });

            // Continue execution of the pipeline
            await next.Execute(context);
        }

        // Updated Faulted method
        public async Task Faulted<TException>(BehaviorExceptionContext<ValidationPipelineSagaState, StartValidationPipeline, TException> context, IBehavior<ValidationPipelineSagaState, StartValidationPipeline> next)
            where TException : Exception
        {
            _logger.LogError(context.Exception, "Error loading steps for pipeline");
            await next.Faulted(context);
        }

        public void Probe(ProbeContext context)
        {
            context.CreateScope("load-validation-steps");
        }

        // Implementation of IVisitable.Accept required by the new interface
        public void Accept(StateMachineVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
