using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record CreateGeneratedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public required GenerateDocumentDTO OriginalDTO { get; set; }
}