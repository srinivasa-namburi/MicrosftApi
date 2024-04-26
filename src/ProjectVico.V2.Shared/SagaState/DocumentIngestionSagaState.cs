using MassTransit;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models.Classification;

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
    public string? DocumentProcessName { get; set; } = "US.NuclearLicensing";
    public string? Plugin { get; set; }
}