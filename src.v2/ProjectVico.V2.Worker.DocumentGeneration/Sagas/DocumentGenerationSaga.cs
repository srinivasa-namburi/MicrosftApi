using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;
using ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;
using ProjectVico.V2.Shared.SagaState;
// ReSharper disable MemberCanBePrivate.Global
// State variables must be public for MassTransit to work

namespace ProjectVico.V2.Worker.DocumentGeneration.Sagas;

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
                    context.Saga.ReactorModel = context.Message.ReactorModel;

                    if (context.Message.Location != null)
                    {
                        context.Saga.LocationLatitude = context.Message.Location.Latitude;
                        context.Saga.LocationLongitude = context.Message.Location.Longitude;
                    }

                    context.Saga.ProjectedProjectStartDate = context.Message.ProjectedProjectStartDate;
                    context.Saga.ProjectedProjectEndDate = context.Message.ProjectedProjectEndDate;
                    context.Saga.CorrelationId = context.Message.Id;
                })
                .Publish(context => new GenerateDocumentOutline(context.Saga.CorrelationId)
                {
                    // Map properties from DocumentGenerationRequest to GenerateDocumentOutline
                    DocumentTitle = context.Message.DocumentTitle,
                    AuthorOid = context.Saga.AuthorOid

                })
                .TransitionTo(Processing));

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
                    GeneratedDocumentJson = context.Message.GeneratedDocumentJson

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
              .If(context =>
                       context.Saga.NumberOfContentNodesGenerated == context.Saga.NumberOfContentNodesToGenerate,
            x => x.TransitionTo(ContentFinalized)));
    }

    public State Processing { get; set; } = null!;
    public State ContentGeneration { get; set; } = null!;
    public State ContentFinalized { get; set; } = null!;
    public Event<DocumentGenerationRequest> DocumentGenerationRequested { get; private set; }
    public Event<DocumentOutlineGenerated> DocumentOutlineGenerated { get; private set; }
    public Event<ReportContentGenerationSubmitted> ReportContentGenerationSubmitted { get; private set; }
    public Event<ContentNodeGenerated> ContentNodeGenerated { get; private set; }

}