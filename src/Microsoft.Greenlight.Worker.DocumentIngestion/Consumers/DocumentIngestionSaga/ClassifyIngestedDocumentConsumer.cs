using System.Globalization;
using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Helpers;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Classification;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

/// <summary>
/// Consumer class for classifying ingested documents.
/// </summary>
public class ClassifyIngestedDocumentConsumer(
    ILogger<ClassifyIngestedDocumentConsumer> logger,
    IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
    DocGenerationDbContext dbContext,
    AzureFileHelper azureFileHelper,
    IServiceProvider serviceProvider
        ) : IConsumer<ClassifyIngestedDocument>
{
    private readonly ILogger<ClassifyIngestedDocumentConsumer> _logger = logger;
    private readonly DocGenerationDbContext _dbContext = dbContext;
    private readonly AzureFileHelper _azureFileHelper = azureFileHelper;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions = serviceConfigurationOptions.Value;

    /// <summary>
    /// Consumes the ClassifyIngestedDocument message and classifies the document.
    /// </summary>
    /// <param name="context">The consume context containing the message.</param>
    public async Task Consume(ConsumeContext<ClassifyIngestedDocument> context)
    {
        var message = context.Message;
        var scope = _serviceProvider.CreateScope();

        var documentProcessOptions = _serviceConfigurationOptions.GreenlightServices.DocumentProcesses.SingleOrDefault(x => x?.Name == message.DocumentProcessName);

        if (documentProcessOptions == null)
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Document process options not found for {ProcessName}", message.DocumentProcessName);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
            return;
        }

        var classifier = scope.ServiceProvider.GetKeyedService<IDocumentClassifier>(message.DocumentProcessName + "-IDocumentClassifier");

        if (classifier == null)
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Classifier not found for {ProcessName}", message.DocumentProcessName);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
            return;
        }

        _logger.LogInformation("ClassifyIngestedDocumentConsumer: Classifying {ProcessName} document {FileName} with correlation id {CorrelationId}", message.DocumentProcessName, message.FileName, message.CorrelationId);

        var ingestedDocument = await _dbContext.IngestedDocuments.FindAsync(message.CorrelationId);
        if (ingestedDocument == null)
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Ingested document not found for correlation id {CorrelationId}", message.CorrelationId);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
            return;
        }

        ingestedDocument.IngestionState = IngestionState.Classifying;
        await _dbContext.SaveChangesAsync();

        var temporaryAccessUrl = _azureFileHelper.GetProxiedBlobUrl(message.OriginalDocumentUrl);

        DocumentClassificationResult? classificationResult;

        // Currently, we only classify documents that do not have a plugin association
        // For other documents, we only classify them if the option is enabled in the owning Document Process.
        if (documentProcessOptions.ClassifyDocuments && ingestedDocument.Plugin == null && classifier != null)
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Classifying Ingested Document {DocumentId}", ingestedDocument.Id);
            classificationResult = await ClassifyDocument(classifier, documentProcessOptions, ingestedDocument, temporaryAccessUrl);
        }
        else
        {
            classificationResult = new DocumentClassificationResult()
            {
                ClassificationShortCode = "no-classification",
                Confidence = 100,
                SuccessfulClassification = true
            };

            ingestedDocument.ClassificationShortCode = "no-classification";
            await _dbContext.SaveChangesAsync();
        }

        if (classificationResult is { SuccessfulClassification: true })
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Document {FileName} classified as {ClassificationType} with confidence {Confidence}",
                               message.FileName, classificationResult.ClassificationShortCode, classificationResult.Confidence.ToString(CultureInfo.InvariantCulture));

            await context.Publish(new IngestedDocumentClassified(message.CorrelationId)
            {
                ClassificationShortCode = classificationResult.ClassificationShortCode
            });

            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Document {FileName} classified as {ClassificationType} with confidence {Confidence}",
                message.FileName, classificationResult.ClassificationShortCode, classificationResult.Confidence);
        }
        else
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Document classification failed for document {FileName}", message.FileName);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
        }
    }

    /// <summary>
    /// Classifies the document using the specified classifier.
    /// </summary>
    /// <param name="classifier">The document classifier.</param>
    /// <param name="documentProcessOptions">The document process options.</param>
    /// <param name="document">The ingested document.</param>
    /// <param name="temporaryAccessUrl">The temporary access URL for the document.</param>
    /// <returns>The classification result.</returns>
    private async Task<DocumentClassificationResult?> ClassifyDocument(
        IDocumentClassifier classifier,
        DocumentProcessOptions documentProcessOptions,
        IngestedDocument document, string temporaryAccessUrl)
    {
        var classificationResult = await classifier.ClassifyDocumentFromUri(
               temporaryAccessUrl,
               documentProcessOptions.ClassificationModelName!);

        if (classificationResult == null) return classificationResult;

        document.ClassificationShortCode = classificationResult.ClassificationShortCode;
        await _dbContext.SaveChangesAsync();

        return classificationResult;
    }
}
