using MassTransit;
using ProjectVico.V2.Shared.Enums;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record ClassifyIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string DocumentProcessName { get; set; }
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public IngestionType IngestionType { get; set; }
}