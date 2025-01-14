using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Sagas;


/// <summary>
/// Represents the state machine for the document ingestion saga.
/// </summary>
public class DocumentIngestionSaga : MassTransitStateMachine<DocumentIngestionSagaState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentIngestionSaga"/> class.
    /// </summary>
#pragma warning disable CS8618 // The MassTransit framework will initialize the event members of this class.
    public DocumentIngestionSaga()
#pragma warning restore CS8618 // The MassTransit framework will initialize the event members of this class.
    {
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
                context.Saga.DocumentLibraryShortName = context.Message.DocumentProcessName;
            })
            .Publish(context => new CreateIngestedDocument(context.Saga.CorrelationId)
            {
                DocumentProcessName = context.Saga.DocumentLibraryShortName,
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
#pragma warning disable CS8601 // Possible null reference assignment.
                // DocumentLibraryShortName is marked as nullable for EF, but has a default value defined in the class
                DocumentProcessName = context.Saga.DocumentLibraryShortName,
#pragma warning restore CS8601 // Possible null reference assignment.
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
                DocumentProcessName = context.Saga.DocumentLibraryShortName,
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

    /// <summary>
    /// Gets the event that occurs when a document is classified.
    /// </summary>
    public Event<IngestedDocumentClassified> IngestedDocumentClassified { get; private set; }

    /// <summary>
    /// Gets the event that occurs when document classification fails.
    /// </summary>
    public Event<IngestedDocumentClassificationFailed> IngestedDocumentClassificationFailed { get; private set; }

    /// <summary>
    /// Gets the event that occurs when document processing fails.
    /// </summary>
    public Event<IngestedDocumentProcessingFailed> IngestedDocumentProcessingFailed { get; private set; }

    /// <summary>
    /// Gets the event that occurs when document processing is stopped due to unsupported classification.
    /// </summary>
    public Event<IngestedDocumentProcessingStoppedByUnsupportedClassification> IngestedDocumentProcessingStoppedByUnsupportedClassification { get; private set; }

    /// <summary>
    /// Gets the event that occurs when a document is processed.
    /// </summary>
    public Event<IngestedDocumentProcessed> IngestedDocumentProcessed { get; private set; }

    /// <summary>
    /// Gets the event that occurs when a document is indexed.
    /// </summary>
    public Event<IngestedDocumentIndexed> IngestedDocumentIndexed { get; private set; }

    /// <summary>
    /// Gets the event that occurs when a document is created in the database.
    /// </summary>
    public Event<IngestedDocumentCreatedInDatabase> IngestedDocumentCreatedInDatabase { get; private set; }

    /// <summary>
    /// Gets the event that occurs when a document is rejected.
    /// </summary>
    public Event<IngestedDocumentRejected> IngestedDocumentRejected { get; private set; }

    /// <summary>
    /// Gets the event that occurs when a classic document ingestion request is made.
    /// </summary>
    public Event<ClassicDocumentIngestionRequest> ClassicDocumentIngestionRequested { get; private set; }

    /// <summary>
    /// Gets or sets the state representing the classifying phase.
    /// </summary>
    public State Classifying { get; set; } = null!;

    /// <summary>
    /// Gets or sets the state representing the creating phase.
    /// </summary>
    public State Creating { get; set; } = null!;

    /// <summary>
    /// Gets or sets the state representing the processing phase.
    /// </summary>
    public State Processing { get; set; } = null!;

    /// <summary>
    /// Gets or sets the state representing the indexing phase.
    /// </summary>
    public State Indexing { get; set; } = null!;
}

