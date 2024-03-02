using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Events;

public record ContentNodeGenerated(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public Guid ContentNodeId { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public string AuthorOid { get; set; }

}