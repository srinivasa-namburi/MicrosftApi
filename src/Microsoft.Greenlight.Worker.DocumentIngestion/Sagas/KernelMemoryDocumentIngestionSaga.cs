using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Events;
using Microsoft.Greenlight.Shared.SagaState;

namespace Microsoft.Greenlight.Worker.DocumentIngestion.Sagas;

/// <summary>
/// Represents the state machine for the Kernel Memory Document Ingestion process.
/// </summary>
public class KernelMemoryDocumentIngestionSaga : MassTransitStateMachine<KernelMemoryDocumentIngestionSagaState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KernelMemoryDocumentIngestionSaga"/> class.
    /// </summary>
#pragma warning disable CS8618 // The MassTransit framework will initialize the event members of this class.
    public KernelMemoryDocumentIngestionSaga()
#pragma warning restore CS8618 // The MassTransit framework will initialize the event members of this class.
    {
        InstanceState(x => x.CurrentState);

        Event(() => KernelMemoryDocumentIngestionRequested,
            x => x.CorrelateById(m => m.Message.CorrelationId));

        Initially(
            When(KernelMemoryDocumentIngestionRequested)
                .Then(context =>
                {
                    context.Saga.CorrelationId = context.Message.CorrelationId;
                    context.Saga.FileName = context.Message.FileName;
                    context.Saga.OriginalDocumentUrl = context.Message.OriginalDocumentUrl;
                    context.Saga.UploadedByUserOid = context.Message.UploadedByUserOid;
                    context.Saga.DocumentLibraryShortName = context.Message.DocumentLibraryShortName;
                    context.Saga.DocumentLibraryType = context.Message.DocumentLibraryType;
                })
                .Publish(context => new KernelMemoryCreateIngestedDocument(context.Message.CorrelationId)
                {
                    DocumentLibraryShortName = context.Saga.DocumentLibraryShortName,
                    FileName = context.Saga.FileName,
                    OriginalDocumentUrl = context.Saga.OriginalDocumentUrl,
                    UploadedByUserOid = context.Saga.UploadedByUserOid,
                    DocumentLibraryType = context.Saga.DocumentLibraryType
                })
                .TransitionTo(Creating)
        );

        During(Creating,
            When(KernelMemoryDocumentCreated)
                .Finalize()
        );

        During(Creating,
            When(KernelMemoryDocumentIngestionCompleted)
                .Finalize()
        );

        During(Creating,
            When(KernelMemoryDocumentIngestionFailed)
                .Finalize()
        );
    }

    /// <summary>
    /// Gets the event that is triggered when a Kernel Memory Document Ingestion is requested.
    /// </summary>
    public Event<KernelMemoryDocumentIngestionRequest> KernelMemoryDocumentIngestionRequested { get; private set; }

    /// <summary>
    /// Gets the event that is triggered when a Kernel Memory Document is created.
    /// </summary>
    public Event<KernelMemoryDocumentCreated> KernelMemoryDocumentCreated { get; private set; }

    /// <summary>
    /// Gets the event that is triggered when a Kernel Memory Document Ingestion fails.
    /// </summary>
    public Event<KernelMemoryDocumentIngestionFailed> KernelMemoryDocumentIngestionFailed { get; private set; }

    /// <summary>
    /// Gets the event that is triggered when a Kernel Memory Document Ingestion is completed.
    /// </summary>
    public Event<KernelMemoryDocumentIngestionCompleted> KernelMemoryDocumentIngestionCompleted { get; private set; }

    /// <summary>
    /// Gets or sets the state representing the creation process.
    /// </summary>
    public State Creating { get; set; } = null!;
}
