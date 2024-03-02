using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record DocumentOutlineGenerated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string GeneratedDocumentJson { get; set; }
    public string? AuthorOid { get; set; }
}