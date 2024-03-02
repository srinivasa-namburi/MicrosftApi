using MassTransit;
using ProjectVico.V2.Shared.Classification.Models;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record ProcessIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public IngestionType IngestionType { get; set; }
    public DocumentClassificationType ClassificationType { get; set; }
}
