using MassTransit;
using ProjectVico.V2.Shared.Classification.Models;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Shared.SagaState;

public class DocumentIngestionSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; }

    public string? FileHash { get; set; }
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }

    public string? ClassificationShortCode { get; set; }
    public DocumentClassificationType? ClassificationType { get; set; }
    public DocumentClassificationSuperType? ClassificationSuperType { get; set; }

    public IngestionState IngestionState { get; set; } = IngestionState.Uploaded;
    public IngestionType IngestionType { get; set; }
}