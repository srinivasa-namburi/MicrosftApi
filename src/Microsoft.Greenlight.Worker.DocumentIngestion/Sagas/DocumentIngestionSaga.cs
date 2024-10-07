using MassTransit;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Sagas;

public class DocumentIngestionSaga : MassTransitStateMachine<DocumentIngestionSagaState>
{
    private readonly ServiceConfigurationOptions _serviceConfiguration;

    public DocumentIngestionSaga(IOptions<ServiceConfigurationOptions> serviceConfigurationOptions)
    {
        _serviceConfiguration = serviceConfigurationOptions.Value;

        InstanceState(x => x.CurrentState);

        Event(() => ClassicDocumentIngestionRequested,
            x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
        When(ClassicDocumentIngestionRequested)
            .Then(context =>
            {
                context.Saga.CorrelationId = context.Message.CorrelationId;
                context.Saga.FileName = context.Message.FileName;
                context.Saga.OriginalDocumentUrl = context.Message.OriginalDocumentUrl;
                context.Saga.UploadedByUserOid = context.Message.UploadedByUserOid;
                context.Saga.DocumentProcessName = context.Message.DocumentProcessName;
                context.Saga.Plugin = context.Message.Plugin;
            })
            .Publish(context => new CreateIngestedDocument(context.Saga.CorrelationId)
            {
                DocumentProcessName = context.Saga.DocumentProcessName,
                FileName = context.Saga.FileName,
                OriginalDocumentUrl = context.Saga.OriginalDocumentUrl,
                UploadedByUserOid = context.Saga.UploadedByUserOid,
                Plugin = context.Saga.Plugin
            })
            .TransitionTo(Creating)
        );

        During(Creating,
        When(IngestedDocumentCreatedInDatabase)
            .Then(context =>
            {
                context.Saga.FileHash = context.Message.FileHash;
            })
            .Publish(context => new ClassifyIngestedDocument(context.Saga.CorrelationId)
            {
                DocumentProcessName = context.Saga.DocumentProcessName,
                OriginalDocumentUrl = context.Saga.OriginalDocumentUrl,
                FileName = context.Saga.FileName,
                UploadedByUserOid = context.Saga.UploadedByUserOid,
                Plugin = context.Saga.Plugin

            })
            .TransitionTo(Classifying)
        );

        During(Creating,
        When(IngestedDocumentRejected)
            .Finalize()
        );

        During(Classifying,
        When(IngestedDocumentClassified)
            .Then(context =>
            {
                context.Saga.ClassificationShortCode = context.Message.ClassificationShortCode;

            })
            .Publish(context => new ProcessIngestedDocument(context.Message.CorrelationId)
            {
                DocumentProcessName = context.Saga.DocumentProcessName,
                OriginalDocumentUrl = context.Saga.OriginalDocumentUrl,
                FileName = context.Saga.FileName,
                UploadedByUserOid = context.Saga.UploadedByUserOid,
                Plugin = context.Saga.Plugin
            })
            .TransitionTo(Processing)
        );

        During(Classifying,
        When(IngestedDocumentClassificationFailed)
            .TransitionTo(Final)
        );

        During(Processing,
        When(IngestedDocumentProcessingFailed)
            .TransitionTo(Final)
        );

        During(Processing,
        When(IngestedDocumentProcessingStoppedByUnsupportedClassification)
            .TransitionTo(Final)
        );

        During(Processing,
        When(IngestedDocumentProcessed)
            .Publish(context => new IndexIngestedDocument(context.Message.CorrelationId))
            .TransitionTo(Indexing)
        );

        During(Indexing,
        When(IngestedDocumentIndexed)
            .Finalize()
        );

        SetCompletedWhenFinalized();

    }

    public Event<IngestedDocumentClassified> IngestedDocumentClassified { get; private set; }
    public Event<IngestedDocumentClassificationFailed> IngestedDocumentClassificationFailed { get; private set; }
    public Event<IngestedDocumentProcessingFailed> IngestedDocumentProcessingFailed { get; private set; }
    public Event<IngestedDocumentProcessingStoppedByUnsupportedClassification> IngestedDocumentProcessingStoppedByUnsupportedClassification { get; private set; }
    public Event<IngestedDocumentProcessed> IngestedDocumentProcessed { get; private set; }
    public Event<IngestedDocumentIndexed> IngestedDocumentIndexed { get; private set; }
    public Event<IngestedDocumentCreatedInDatabase> IngestedDocumentCreatedInDatabase { get; private set; }
    public Event<IngestedDocumentRejected> IngestedDocumentRejected { get; private set; }

    public Event<ClassicDocumentIngestionRequest> ClassicDocumentIngestionRequested { get; private set; }

    public State Classifying { get; set; } = null!;
    public State Creating { get; set; } = null!;
    public State Processing { get; set; } = null!;
    public State Indexing { get; set; } = null!;

    private string GetIngestionMethod(string documentProcessName)
    {
        var documentProcess = _serviceConfiguration.GreenlightServices.DocumentProcesses
            .FirstOrDefault(process => process?.Name == documentProcessName);

        return documentProcess?.IngestionMethod ?? string.Empty;
    }
}

