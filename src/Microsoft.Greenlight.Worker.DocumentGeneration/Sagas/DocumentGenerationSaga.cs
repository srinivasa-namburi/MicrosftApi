using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;
using Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events;
using Microsoft.Greenlight.Shared.SagaState;
// ReSharper disable MemberCanBePrivate.Global
// State variables must be public for MassTransit to work

namespace Microsoft.Greenlight.Worker.DocumentGeneration.Sagas;

/// <summary>
/// The DocumentGenerationSaga class is the Mass Transit state machine responsible for coordinating the generation of a
/// document.
/// </summary>
public class DocumentGenerationSaga : MassTransitStateMachine<DocumentGenerationSagaState>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentGenerationSaga"/> class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The constructor is responsible for creating the Mass Transit messaging flow for generating the document.
    /// </para>
    /// </remarks>
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
              .If(context => context.Saga.NumberOfContentNodesGenerated == context.Saga.NumberOfContentNodesToGenerate,
                    x => x.TransitionTo(ContentFinalized)));

    }

    /// <summary>
    /// Gets or sets the Processing <see cref="State"/> and its associated events.
    /// </summary>
    public State Processing { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Creating <see cref="State"/> and its associated events.
    /// </summary>
    public State Creating { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ContentGeneration <see cref="State"/> and its associated events.
    /// </summary>
    public State ContentGeneration { get; set; } = null!;

    /// <summary>
    /// Gets or sets the ContentFinalized <see cref="State"/> and its associated events.
    /// </summary>
    public State ContentFinalized { get; set; } = null!;

    /// <summary>
    /// Gets the document generation requested <see cref="Event"/>.
    /// </summary>
    public Event<GenerateDocumentDTO> DocumentGenerationRequested { get; private set; } = null!;

    /// <summary>
    /// Gets the document created <see cref="Event"/>.
    /// </summary>
    public Event<GeneratedDocumentCreated> GeneratedDocumentCreated { get; private set; } = null!;

    /// <summary>
    /// Gets the document outline generated <see cref="Event"/>.
    /// </summary>
    public Event<DocumentOutlineGenerated> DocumentOutlineGenerated { get; private set; } = null!;

    /// <summary>
    /// Gets the document outline generation failed <see cref="Event"/>.
    /// </summary>
    public Event<DocumentOutlineGenerationFailed> DocumentOutlineGenerationFailed { get; private set; } = null!;

    /// <summary>
    /// Gets the report content generation submitted <see cref="Event"/>.
    /// </summary>
    public Event<ReportContentGenerationSubmitted> ReportContentGenerationSubmitted { get; private set; } = null!;

    /// <summary>
    /// Gets the content node generated <see cref="Event"/>.
    /// </summary>
    public Event<ContentNodeGenerated> ContentNodeGenerated { get; private set; } = null!;
}
