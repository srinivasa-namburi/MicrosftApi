using MassTransit;
using Microsoft.Extensions.Options;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Events;
using ProjectVico.V2.Shared.SagaState;

namespace ProjectVico.V2.Worker.DocumentIngestion.Sagas;

public class KernelMemoryDocumentIngestionSaga : MassTransitStateMachine<KernelMemoryDocumentIngestionSagaState>
{
    private readonly ServiceConfigurationOptions _serviceConfiguration;

    public KernelMemoryDocumentIngestionSaga(IOptions<ServiceConfigurationOptions> serviceConfigurationOptions)
    {
        _serviceConfiguration = serviceConfigurationOptions.Value;

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
                    context.Saga.DocumentProcessName = context.Message.DocumentProcessName;
                    context.Saga.Plugin = context.Message.Plugin;
                })
                .Publish(context => new KernelMemoryCreateIngestedDocument(context.Message.CorrelationId)
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

    public Event<KernelMemoryDocumentIngestionRequest> KernelMemoryDocumentIngestionRequested { get; private set; }
    public Event<KernelMemoryDocumentCreated> KernelMemoryDocumentCreated { get; private set; }
    public Event<KernelMemoryDocumentIngestionFailed> KernelMemoryDocumentIngestionFailed { get; private set; }
    public Event<KernelMemoryDocumentIngestionCompleted> KernelMemoryDocumentIngestionCompleted { get; private set; }

    public State Creating { get; set; } = null!;
}