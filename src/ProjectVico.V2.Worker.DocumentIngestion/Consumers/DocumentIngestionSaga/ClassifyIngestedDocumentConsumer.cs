using System.Globalization;
using Humanizer;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.DocumentProcess.Shared.Ingestion.Classification.Classifiers;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Classification;

namespace ProjectVico.V2.Worker.DocumentIngestion.Consumers.DocumentIngestionSaga;

public class ClassifyIngestedDocumentConsumer : IConsumer<ClassifyIngestedDocument>
{
    private readonly ILogger<ClassifyIngestedDocumentConsumer> _logger;
    private IDocumentClassifier? _classifier;
    private readonly DocGenerationDbContext _dbContext;
    private readonly AzureFileHelper _azureFileHelper;
    private readonly IServiceProvider _serviceProvider;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private DocumentProcessOptions? _documentProcessOptions;

    public ClassifyIngestedDocumentConsumer(
        ILogger<ClassifyIngestedDocumentConsumer> logger,
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        DocGenerationDbContext dbContext,
        AzureFileHelper azureFileHelper,
        IServiceProvider serviceProvider
        )
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _logger = logger;
        _dbContext = dbContext;
        _azureFileHelper = azureFileHelper;
        _serviceProvider = serviceProvider;
    }

    public async Task Consume(ConsumeContext<ClassifyIngestedDocument> context)
    {
        var message = context.Message;
        var scope = _serviceProvider.CreateScope();

        _documentProcessOptions = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.SingleOrDefault(x => x?.Name == message.DocumentProcessName);

        if (_documentProcessOptions == null)
        {
            _logger.LogWarning("ClassifyIngestedDocumentConsumer: Document process options not found for {ProcessName}", message.DocumentProcessName);
            await context.Publish(new IngestedDocumentClassificationFailed(message.CorrelationId));
            return;
        }

        _classifier = scope.ServiceProvider.GetKeyedService<IDocumentClassifier>(message.DocumentProcessName+"-IDocumentClassifier");

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
        if (_documentProcessOptions.ClassifyDocuments && ingestedDocument.Plugin == null && _classifier != null)
        {
            _logger.LogInformation("ClassifyIngestedDocumentConsumer: Classifying Ingested Document {DocumentId}", ingestedDocument.Id);
            classificationResult = await ClassifyDocument(message, ingestedDocument, temporaryAccessUrl);
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

    private async Task<DocumentClassificationResult?> ClassifyDocument(ClassifyIngestedDocument message, IngestedDocument document, string temporaryAccessUrl)
    {

        var classificationResult = await _classifier.ClassifyDocumentFromUri(
               temporaryAccessUrl,
               _documentProcessOptions.ClassificationModelName!);

        if (classificationResult == null) return classificationResult;

        document.ClassificationShortCode = classificationResult.ClassificationShortCode;
        await _dbContext.SaveChangesAsync();

        return classificationResult;
    }
}