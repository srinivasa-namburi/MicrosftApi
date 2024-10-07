using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.Interfaces;
using Microsoft.Greenlight.Shared.SagaState;
// ReSharper disable MemberCanBePrivate.Global
// State variables must be public for MassTransit to work

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Sagas;

public class DocumentGenerationSaga : MassTransitStateMachine<DocumentGenerationSagaState>
{
    public DocumentGenerationSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => DocumentGenerationRequested,
            x => x.CorrelateById(m => m.Message.Id));

        Initially(
        When(DocumentGenerationRequested)
            .Then(context =>
            {
                context.Saga.DocumentTitle = context.Message.DocumentTitle;
                context.Saga.AuthorOid = context.Message.AuthorOid;
       
                context.Saga.DocumentProcessName = context.Message.DocumentProcessName;
                context.Saga.CorrelationId = context.Message.Id;
                context.Saga.MetadataJson = context.Message.RequestAsJson;
            })
            .Publish(context=> new CreateGeneratedDocument(context.Saga.CorrelationId)
            {
                OriginalDTO = context.Message
            })
            .TransitionTo(Creating));
    
        During(Creating,
        When(GeneratedDocumentCreated)
            .Then(context =>
            {
                context.Saga.MetadataId = context.Message.MetaDataId;
            })
            .Publish(context => new GenerateDocumentOutline(context.Saga.CorrelationId)
            {
                DocumentTitle = context.Saga.DocumentTitle,
                AuthorOid = context.Saga.AuthorOid,
                DocumentProcess = context.Saga.DocumentProcessName
            })
            .TransitionTo(Processing));

        During(Processing,
        When(DocumentOutlineGenerationFailed)
            .Finalize()
        );

        During(Processing,
        When(DocumentOutlineGenerated)
            .Then(context =>
            {
                // Handle the completion of document outline generation
                // Maybe update the saga state with details from GeneratedDocument
            })
            //.TransitionTo(ContentFinalized));
            .Publish(context => new GenerateReportContent(context.Saga.CorrelationId)
            {
                AuthorOid = context.Saga.AuthorOid,
                GeneratedDocumentJson = context.Message.GeneratedDocumentJson,
                DocumentProcess = context.Saga.DocumentProcessName,
                MetadataId = context.Saga.MetadataId
            })
            .TransitionTo(ContentGeneration));

        During(ContentGeneration,
        When(ReportContentGenerationSubmitted)
            .Then(context =>
            {
                context.Saga.NumberOfContentNodesToGenerate = context.Message.NumberOfContentNodesToGenerate;
                context.Saga.NumberOfContentNodesGenerated = 0;
            }));

        // For each content node generated, we will receive a ContentNodeGenerated event
        // We will transition to ContentFinalized when all content nodes have been generated
        During(ContentGeneration,
        When(ContentNodeGenerated)
              .Then(context =>
              {
                  if (context.Message.IsSuccessful)
                  {
                      context.Saga.NumberOfContentNodesGenerated++;
                  }
              })
              .If(context =>  context.Saga.NumberOfContentNodesGenerated == context.Saga.NumberOfContentNodesToGenerate,
                    x => x.TransitionTo(ContentFinalized)));

    }

    public State Processing { get; set; } = null!;
    public State Creating { get; set; } = null!;
    public State ContentGeneration { get; set; } = null!;
    public State ContentFinalized { get; set; } = null!;
    public Event<GenerateDocumentDTO> DocumentGenerationRequested { get; private set; }
    public Event<GeneratedDocumentCreated> GeneratedDocumentCreated { get; private set; }
    public Event<DocumentOutlineGenerated> DocumentOutlineGenerated { get; private set; }
    public Event<DocumentOutlineGenerationFailed> DocumentOutlineGenerationFailed { get; private set; }
    public Event<ReportContentGenerationSubmitted> ReportContentGenerationSubmitted { get; private set; }
    public Event<ContentNodeGenerated> ContentNodeGenerated { get; private set; }

}
