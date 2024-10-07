using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record KernelMemoryDocumentIngestionRequest (Guid CorrelationId) : CorrelatedBy<Guid>
{
    
    public string FileName { get; set; }
    public string OriginalDocumentUrl { get; set; }
    public string? UploadedByUserOid { get; set; }
    public string DocumentProcessName { get; set; }
    public string? Plugin { get; set; }
}
