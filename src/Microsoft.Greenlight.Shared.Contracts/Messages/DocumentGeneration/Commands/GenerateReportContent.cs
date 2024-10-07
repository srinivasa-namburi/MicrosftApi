using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record GenerateReportContent(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string? GeneratedDocumentJson { get; set; }
    public string? AuthorOid { get; set; }
    public string? DocumentProcess { get; set; }
    public Guid? MetadataId { get; set; }
}
