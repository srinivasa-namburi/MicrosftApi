using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services.Validation;

namespace Microsoft.Greenlight.Worker.Validation.Consumers
{
    /// <summary>
    /// Executes a validation step in the validation pipeline, selecting the correct logic factory based on the step type.
    /// </summary>
    public class ExecuteValidationStepConsumer : IConsumer<ExecuteValidationStep>
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ValidationStepExecutionLogicFactory _executionStepLogicFactory;
        private readonly ILogger<ExecuteValidationStepConsumer> _logger;

        /// <summary>
        /// Main constructor
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="executionStepLogicFactory"></param>
        /// <param name="logger"></param>
        public ExecuteValidationStepConsumer(
            DocGenerationDbContext dbContext, 
            ValidationStepExecutionLogicFactory executionStepLogicFactory,
            ILogger<ExecuteValidationStepConsumer> logger)
        {
            _dbContext = dbContext;
            _executionStepLogicFactory = executionStepLogicFactory;
            _logger = logger;
        }

        /// <summary>
        /// Executes a validation step in the validation pipeline, selecting the correct logic factory based on the step type.
        /// </summary>
        public async Task Consume(ConsumeContext<ExecuteValidationStep> context)
        {
            var stepId = context.Message.ValidationPipelineExecutionStepId;
            var step = await _dbContext.ValidationPipelineExecutionSteps
                .FirstOrDefaultAsync(x => x.Id == stepId);

            if (step == null)
            {
                _logger.LogError("Step {StepId} not found", stepId);
                await context.Publish(new ValidationStepFailed(context.Message.CorrelationId)
                {
                    ValidationPipelineExecutionStepId = stepId,
                    ErrorMessage = "Step not found"
                });
                return;
            }

            try
            {
                // Use the logic factory to get the correct execution logic for the step
                var logic = await _executionStepLogicFactory.GetExecutionLogicForValidationStepAsync(context.Message);

                // Execute the step
                _logger.LogInformation("Executing Validation step {StepId} for Execution Type {StepExecutionType}", stepId, step.PipelineExecutionType.ToString());
                step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.InProgress;
                await _dbContext.SaveChangesAsync();

                await logic.ExecuteAsync(context.Message);

                // Update step status in the database
                step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.Done;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Successfully completed step {StepId}", stepId);
                await context.Publish(new ValidationStepCompleted(context.Message.CorrelationId)
                {
                    ValidationPipelineExecutionStepId = stepId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing step {StepId}", stepId);
                step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.Failed;
                await _dbContext.SaveChangesAsync();

                await context.Publish(new ValidationStepFailed(context.Message.CorrelationId)
                {
                    ValidationPipelineExecutionStepId = stepId,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
