using MassTransit;

namespace ProjectVico.V2.Shared.Contracts.Messages.DocumentGeneration.Commands;

public record GenerateReportContent(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string? GeneratedDocumentJson { get; set; }
    public string? AuthorOid { get; set; }
}