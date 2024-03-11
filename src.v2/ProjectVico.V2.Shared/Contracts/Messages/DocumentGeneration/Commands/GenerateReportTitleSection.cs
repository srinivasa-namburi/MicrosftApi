using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record GenerateReportTitleSection(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string ContentNodeJson { get; set; }
    public string DocumentOutlineJson { get; set; }
    public string AuthorOid { get; set; }
}