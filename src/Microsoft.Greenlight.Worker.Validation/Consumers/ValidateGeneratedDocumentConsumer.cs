using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.Validation.Commands;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models.Validation;
using Microsoft.Greenlight.Shared.Services;

namespace Microsoft.Greenlight.Worker.Validation.Consumers
{

    /// <summary>
    /// Consumes the ValidateReportContent message and generates a full document text for the document.
    /// Validation is then started according to the attached document process and its validation pipeline.
    /// </summary>
    public class ValidateGeneratedDocumentConsumer : IConsumer<ValidateGeneratedDocument>
    {
        private readonly DocGenerationDbContext _dbContext;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbContext">DocGenerationDbContext</param>
        /// <param name="documentProcessInfoService">The Document Process Info Service</param>
        public ValidateGeneratedDocumentConsumer(
            DocGenerationDbContext dbContext, 
            IDocumentProcessInfoService documentProcessInfoService)
        {
            _dbContext = dbContext;
            _documentProcessInfoService = documentProcessInfoService;
        }
        
        /// <summary>
        /// Consumes the ValidateReportContent message and generates a full document text for the document.
        /// Validation is then started according to the attached document process and its validation pipeline.
        /// </summary>
        /// <param name="context">Mass Transit context</param>
        public async Task Consume(ConsumeContext<ValidateGeneratedDocument> context)
        {
            var message = context.Message;

            // Determine the pipeline steps for validation and generate a new validation execution
            var generatedDocument = await _dbContext.GeneratedDocuments
                .FirstOrDefaultAsync(x => x.Id == message.CorrelationId);

            if (generatedDocument != null && !string.IsNullOrEmpty(generatedDocument.DocumentProcess))
            {
                var documentProcessInfo =
                    await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(
                        generatedDocument.DocumentProcess);

                if (documentProcessInfo is { ValidationPipelineId: not null })
                {
                    var documentProcessValidationPipeline = await _dbContext.DocumentProcessValidationPipelines
                        .Where(x => x.Id == documentProcessInfo.ValidationPipelineId)
                        .Include(x => x.ValidationPipelineSteps)
                        .FirstOrDefaultAsync();

                    var validationExecution = new ValidationPipelineExecution()
                    {
                        Id = Guid.NewGuid(),
                        GeneratedDocumentId = generatedDocument.Id,
                        DocumentProcessValidationPipelineId = (Guid)documentProcessInfo.ValidationPipelineId,
                    };

                    if (documentProcessValidationPipeline?.ValidationPipelineSteps != null)
                    {
                        foreach (var step in documentProcessValidationPipeline?.ValidationPipelineSteps!)
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

                    await _dbContext.ValidationPipelineExecutions.AddAsync(validationExecution);
                    await _dbContext.SaveChangesAsync();

                    await context.Publish(new StartValidationPipeline(validationExecution.Id)
                    {
                        GeneratedDocumentId = generatedDocument.Id
                    });
                }
                else
                {
                    //TODO: Log that the document process does not have a validation pipeline and send a failure message + notification
                }
            }
        }

        
    }

}