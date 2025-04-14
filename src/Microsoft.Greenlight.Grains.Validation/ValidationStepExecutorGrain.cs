using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Grains.Validation.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Services.Validation;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Validation
{
    [Reentrant]
    public class ValidationStepExecutorGrain : Grain, IValidationStepExecutorGrain
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ValidationStepExecutionLogicFactory _executionLogicFactory;
        private readonly ILogger<ValidationStepExecutorGrain> _logger;

        public ValidationStepExecutorGrain(
            DocGenerationDbContext dbContext,
            ValidationStepExecutionLogicFactory executionLogicFactory,
            ILogger<ValidationStepExecutorGrain> logger)
        {
            _dbContext = dbContext;
            _executionLogicFactory = executionLogicFactory;
            _logger = logger;
        }

        public async Task<ValidationStepResult> ExecuteStepAsync(
            Guid validationExecutionId, 
            ValidationPipelineExecutionType executionType)
        {
            var stepId = this.GetPrimaryKey();

            try
            {
                var step = await _dbContext.ValidationPipelineExecutionSteps
                    .FirstOrDefaultAsync(x => x.Id == stepId);

                if (step == null)
                {
                    _logger.LogError("Step {StepId} not found", stepId);
                    return ValidationStepResult.Failure("Step not found");
                }

                // Update step status in database
                step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.InProgress;
                await _dbContext.SaveChangesAsync();

                // Create execution message
                var executionMessage = new ExecuteValidationStep(validationExecutionId)
                {
                    ValidationPipelineExecutionStepId = stepId,
                    ExecutionType = executionType
                };

                // Get the appropriate execution logic
                var logic = await _executionLogicFactory.GetExecutionLogicForValidationStepAsync(executionMessage);

                // Execute the step
                _logger.LogInformation("Executing validation step {StepId} for execution type {ExecutionType}",
                    stepId, executionType);

                await logic.ExecuteAsync(executionMessage);

                // Update step status in the database
                step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.Done;
                await _dbContext.SaveChangesAsync();

                return ValidationStepResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing validation step {StepId}", stepId);

                try
                {
                    // Try to update step status
                    var step = await _dbContext.ValidationPipelineExecutionSteps
                        .FirstOrDefaultAsync(x => x.Id == stepId);
                    if (step != null)
                    {
                        step.PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.Failed;
                        await _dbContext.SaveChangesAsync();
                    }
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Error updating step status for step {StepId}", stepId);
                }

                return ValidationStepResult.Failure(ex.Message);
            }
        }

    }
}
