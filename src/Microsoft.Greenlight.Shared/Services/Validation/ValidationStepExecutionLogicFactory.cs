using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Services.Validation
{
    public class ValidationStepExecutionLogicFactory
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IServiceProvider _sp;

        public ValidationStepExecutionLogicFactory(
            DocGenerationDbContext dbContext,
            IServiceProvider sp
        )
        {
            _dbContext = dbContext;
            _sp = sp;
        }

        public async Task<IValidationStepExecutionLogic> GetExecutionLogicForValidationStepAsync(
            ExecuteValidationStep step)
        {
            var validationExecutionStep = await _dbContext.ValidationPipelineExecutionSteps
                .Where(x => x.Id == step.ValidationPipelineExecutionStepId)
                .Include(x => x.ValidationPipelineExecution)
                    .ThenInclude(x => x!.DocumentProcessValidationPipeline)
                        .ThenInclude(x => x!.DocumentProcess)
                .AsNoTracking()
                .AsSplitQuery()
                .FirstOrDefaultAsync();

            if (validationExecutionStep == null)
            {
                throw new InvalidOperationException(
                    $"Validation step with ID {step.ValidationPipelineExecutionStepId} not found");
            }

            return validationExecutionStep.PipelineExecutionType switch
            {
                ValidationPipelineExecutionType.ParallelFullDocument =>
                    _sp.GetRequiredKeyedService<IValidationStepExecutionLogic>(nameof(ParallelFullDocumentValidationStepExecutionLogic)),
                ValidationPipelineExecutionType.SequentialFullDocument =>
                    _sp.GetRequiredKeyedService<IValidationStepExecutionLogic>(nameof(SequentialFullDocumentValidationStepExecutionLogic)),
                ValidationPipelineExecutionType.ParallelByOuterChapter =>
                    _sp.GetRequiredKeyedService<IValidationStepExecutionLogic>(nameof(ParallelByOuterChapterValidationStepExecutionLogic)),
                _ => throw new InvalidOperationException(
                    $"Unknown validation step execution type {validationExecutionStep.PipelineExecutionType}")
            };
        }
    }
}