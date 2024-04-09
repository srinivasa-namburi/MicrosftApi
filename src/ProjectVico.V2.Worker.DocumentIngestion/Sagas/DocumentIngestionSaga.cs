using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.SagaState;

namespace ProjectVico.V2.Worker.DocumentIngestion.Sagas;

public class DocumentIngestionSaga : MassTransitStateMachine<DocumentIngestionSagaState>
{
    public DocumentIngestionSaga()
    {

        InstanceState(x => x.CurrentState);

        Event(() => DocumentIngestionRequested,
            x => x.CorrelateById(m => m.Message.Id));

        Initially(
        When(DocumentIngestionRequested)
            .Then(context =>
            {
                context.Saga.CorrelationId = context.Message.Id;
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

    public Event<DocumentIngestionRequest> DocumentIngestionRequested { get; private set; }

    public State Classifying { get; set; } = null!;
    public State Creating { get; set; } = null!;
    public State Processing { get; set; } = null!;
    public State Indexing { get; set; } = null!;


}