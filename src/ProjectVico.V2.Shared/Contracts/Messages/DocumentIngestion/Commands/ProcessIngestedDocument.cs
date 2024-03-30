using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record ProcessIngestedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public string? DocumentProcessName { get; set; }
}
