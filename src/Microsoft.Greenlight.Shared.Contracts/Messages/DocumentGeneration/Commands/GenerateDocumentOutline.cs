using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record GenerateDocumentOutline(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }
    public string? DocumentProcess { get; set; }
}
