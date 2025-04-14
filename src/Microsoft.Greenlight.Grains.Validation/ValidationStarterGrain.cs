using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Validation.Contracts;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Validation;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Validation
{
    [StatelessWorker]
    public class ValidationStarterGrain : Grain, IValidationStarterGrain
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<ValidationStarterGrain> _logger;

        public ValidationStarterGrain(
            DocGenerationDbContext dbContext,
            ILogger<ValidationStarterGrain> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<Guid> StartValidationForDocumentAsync(Guid generatedDocumentId)
        {
            try
            {
                _logger.LogInformation("Starting validation process for document {DocumentId}", generatedDocumentId);

                // Get the document
                var generatedDocument = await _dbContext.GeneratedDocuments
                    .FirstOrDefaultAsync(x => x.Id == generatedDocumentId);

                if (generatedDocument == null || string.IsNullOrEmpty(generatedDocument.DocumentProcess))
                {
                    _logger.LogError("Document {DocumentId} not found or has no document process", generatedDocumentId);
                    throw new ArgumentException($"Document {generatedDocumentId} not found or has no document process");
                }

                // Mark any previous unapplied validations as abandoned
                var previousValidations = await _dbContext.ValidationPipelineExecutions
                    .Where(v => v.GeneratedDocumentId == generatedDocumentId &&
                                v.ApplicationStatus == ValidationPipelineExecutionApplicationStatus.Unapplied)
                    .ToListAsync();

                foreach (var validation in previousValidations)
                {
                    validation.ApplicationStatus = ValidationPipelineExecutionApplicationStatus.Abandoned;
                }

                if (previousValidations.Any())
                {
                    await _dbContext.SaveChangesAsync();
                }

                // Get document process definition
                var documentProcessDefinition = await _dbContext.DynamicDocumentProcessDefinitions
                    .Where(x => x.ShortName == generatedDocument.DocumentProcess)
                    .FirstOrDefaultAsync();

                if (documentProcessDefinition == null || documentProcessDefinition.ValidationPipelineId == null)
                {
                    _logger.LogError("Document process {ProcessName} not found or has no validation pipeline", 
                        generatedDocument.DocumentProcess);
                    throw new ArgumentException($"Document process {generatedDocument.DocumentProcess} not found or has no validation pipeline");
                }

                // Get validation pipeline
                var documentProcessValidationPipeline = await _dbContext.DocumentProcessValidationPipelines
                    .Where(x => x.Id == documentProcessDefinition.ValidationPipelineId)
                    .Include(x => x.ValidationPipelineSteps)
                    .FirstOrDefaultAsync();

                if (documentProcessValidationPipeline == null)
                {
                    _logger.LogError("Validation pipeline {PipelineId} not found", documentProcessDefinition.ValidationPipelineId);
                    throw new ArgumentException($"Validation pipeline {documentProcessDefinition.ValidationPipelineId} not found");
                }

                // Create validation execution record
                var validationExecution = new ValidationPipelineExecution()
                {
                    Id = Guid.NewGuid(),
                    GeneratedDocumentId = generatedDocument.Id,
                    DocumentProcessValidationPipelineId = (Guid)documentProcessDefinition.ValidationPipelineId,
                    ApplicationStatus = ValidationPipelineExecutionApplicationStatus.Unapplied
                };

                // Create execution steps
                if (documentProcessValidationPipeline.ValidationPipelineSteps != null)
                {
                    foreach (var step in documentProcessValidationPipeline.ValidationPipelineSteps)
                    {
                        var executionStep = new ValidationPipelineExecutionStep()
                        {
                            Id = Guid.NewGuid(),
                            ValidationPipelineExecution = validationExecution,
                            ValidationPipelineExecutionId = validationExecution.Id,
                            PipelineExecutionType = step.PipelineExecutionType,
                            PipelineExecutionStepStatus = ValidationPipelineExecutionStepStatus.NotStarted,
                            Order = step.Order
                        };
                        validationExecution.ExecutionSteps.Add(executionStep);
                    }
                }

                // Save to database
                await _dbContext.ValidationPipelineExecutions.AddAsync(validationExecution);
                await _dbContext.SaveChangesAsync();

                // Activate the validation pipeline orchestration grain
                var orchestrationGrain = GrainFactory.GetGrain<IValidationPipelineOrchestrationGrain>(validationExecution.Id);
                await orchestrationGrain.StartValidationPipelineAsync(generatedDocument.Id);

                return validationExecution.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting validation for document {DocumentId}", generatedDocumentId);
                throw;
            }
        }
    }
}
