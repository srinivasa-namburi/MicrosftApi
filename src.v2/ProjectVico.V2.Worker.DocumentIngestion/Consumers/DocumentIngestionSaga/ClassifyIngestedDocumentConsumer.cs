using System.Globalization;
using Humanizer;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Classification.Classifiers;
using ProjectVico.V2.Shared.Classification.Models;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

public class ClassifyIngestedDocumentConsumer : IConsumer<ClassifyIngestedDocument>
{
    private readonly ILogger<ClassifyIngestedDocumentConsumer> _logger;
    private readonly IDocumentClassifier _nrcClassifier;
    private readonly IDocumentClassifier _customDataClassifier;
    private readonly DocGenerationDbContext _dbContext;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;

    public ClassifyIngestedDocumentConsumer(
        ILogger<ClassifyIngestedDocumentConsumer> logger,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("nrc-classifier")] IDocumentClassifier nrcClassifier,
        [FromKeyedServices("customdata-classifier")] IDocumentClassifier customDataClassifier,
        DocGenerationDbContext dbContext,
        AzureFileHelper azureFileHelper


        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
        _nrcClassifier = nrcClassifier;
        _customDataClassifier = customDataClassifier;
        _dbContext = dbContext;
        _azureFileHelper = azureFileHelper;
    }

    public async Task Consume(ConsumeContext<ClassifyIngestedDocument> context)
    {
        var message = context.Message;

        var ingestedDocument = await _dbContext.IngestedDocuments.FindAsync(message.CorrelationId);
        if (ingestedDocument == null)
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Ingested document not found for correlation id {CorrelationId}", message.CorrelationId);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
            return;
        }

        ingestedDocument.IngestionState = IngestionState.Classifying;
        await _dbContext.SaveChangesAsync();

        // Get temporary access URL for the ingested document
        var temporaryAccessUrl = _azureFileHelper.GetTemporaryFileUrl(
            message.OriginalDocumentUrl,
            15.Minutes(),
            message.IngestionType
        );

        DocumentClassificationResult? classificationResult;

        // Handle classification of ingested document according to its type
        if (message.IngestionType == IngestionType.NRCDocument &&
            _serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.Classification.ClassifyNRCDocuments)
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Classifying NRC document {DocumentId}", ingestedDocument.Id);
            classificationResult = await ClassifyNRCDocument(message, temporaryAccessUrl);
        }
        else if (message.IngestionType == IngestionType.CustomData &&
            _serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.Classification.ClassifyCustomDataDocuments)
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Classifying CustomData document {DocumentId}", ingestedDocument.Id);
            classificationResult = await ClassifyCustomData(message, temporaryAccessUrl);
        }
        else
        {
            classificationResult = new DocumentClassificationResult()
            {
                ClassificationType = DocumentClassificationType.CustomDataBasicDocument,
                ClassificationShortCode = "er-numberedchapters",
                Confidence = 100,
                SuccessfulClassification = true
            };
        }



        if (classificationResult != null)
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Document {FileName} classified as {ClassificationType} with confidence {Confidence}",
                               message.FileName, classificationResult.ClassificationType.ToString(), classificationResult.Confidence.ToString(CultureInfo.InvariantCulture));
            await context.Publish(new IngestedDocumentClassified(message.CorrelationId)
            {
                ClassificationShortCode = classificationResult.ClassificationShortCode,
                ClassificationType = classificationResult.ClassificationType,
            });
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Document {FileName} classified as {ClassificationType} with confidence {Confidence}",
                message.FileName, classificationResult.ClassificationType, classificationResult.Confidence);
        }
        else
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Document classification failed for document {FileName}", message.FileName);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
        }
    }

    private async Task<DocumentClassificationResult?> ClassifyCustomData(ClassifyIngestedDocument message, string temporaryAccessUrl)
    {
        if (_serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.Classification
            .ClassifyCustomDataDocuments)
        {
            var classificationResult = await _customDataClassifier.ClassifyDocumentFromUri(
                                              temporaryAccessUrl, _serviceConfigurationOptions.ProjectVicoServices
                                                  .DocumentIngestion.Classification.CustomDataClassificationModelName);
            return classificationResult;
        }

        return null;
    }

    private async Task<DocumentClassificationResult?> ClassifyNRCDocument(ClassifyIngestedDocument message, string temporaryAccessUrl)
    {
        if (_serviceConfigurationOptions.ProjectVicoServices.DocumentIngestion.Classification
            .ClassifyNRCDocuments)
        {
            var classificationResult = await _nrcClassifier.ClassifyDocumentFromUri(
                temporaryAccessUrl,
                _serviceConfigurationOptions.ProjectVicoServices
                    .DocumentIngestion.Classification.NRCClassificationModelName);

            return classificationResult;
        }

        return null;
    }
}