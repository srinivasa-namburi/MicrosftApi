using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Grains.Validation.Contracts.Models;
using Microsoft.Greenlight.Grains.Validation.Contracts.State;
using Microsoft.Greenlight.Shared.Data.Sql;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Validation
{
    [Reentrant]
    public class ValidationStepsLoaderGrain : Grain, IValidationStepsLoaderGrain
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<ValidationStepsLoaderGrain> _logger;

        public ValidationStepsLoaderGrain(
            DocGenerationDbContext dbContext,
            ILogger<ValidationStepsLoaderGrain> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<ValidationStepResult<List<ValidationPipelineStepInfo>>> LoadValidationStepsAsync(
            Guid validationExecutionId, Guid generatedDocumentId)
        {
            try
            {
                _logger.LogInformation("Loading validation steps for execution {ExecutionId}", validationExecutionId);

                var execution = await _dbContext.ValidationPipelineExecutions
                    .Include(x => x.ExecutionSteps)
                    .FirstOrDefaultAsync(x => x.Id == validationExecutionId);

                if (execution == null)
                {
                    _logger.LogError("Validation execution {ExecutionId} not found", validationExecutionId);
                    return ValidationStepResult<List<ValidationPipelineStepInfo>>.Failure("Validation execution not found");
                }

                var orderedSteps = execution.ExecutionSteps
                    .OrderBy(x => x.Order)
                    .Select(x => new ValidationPipelineStepInfo
                    {
                        StepId = x.Id,
                        Order = x.Order,
                        ExecutionType = x.PipelineExecutionType
                    })
                    .ToList();
            
                return ValidationStepResult<List<ValidationPipelineStepInfo>>.Success(orderedSteps);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading validation steps for execution {ExecutionId}", validationExecutionId);
                return ValidationStepResult<List<ValidationPipelineStepInfo>>.Failure(
                    $"Failed to load validation steps: {ex.Message}");
            }
        }
    }
}
