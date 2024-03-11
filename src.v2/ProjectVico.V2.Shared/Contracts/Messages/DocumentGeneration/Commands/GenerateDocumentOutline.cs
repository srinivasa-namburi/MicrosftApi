using MassTransit;
using ProjectVico.V2.Shared.Contracts.DTO;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record GenerateDocumentOutline(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string DocumentTitle { get; set; }
    public string? AuthorOid { get; set; }
    public string? DocumentProcess { get; set; }
}