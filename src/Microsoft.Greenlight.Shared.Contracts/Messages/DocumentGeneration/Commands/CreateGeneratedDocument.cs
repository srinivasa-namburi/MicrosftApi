using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record CreateGeneratedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required GenerateDocumentDTO OriginalDTO { get; set; }
}
