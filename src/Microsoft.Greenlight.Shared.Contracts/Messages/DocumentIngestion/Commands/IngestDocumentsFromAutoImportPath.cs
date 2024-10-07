using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands;

public record IngestDocumentsFromAutoImportPath(Guid CorrelationId) : CorrelatedBy<Guid>
{
    public string ContainerName { get; set; }
    public string FolderPath { get; set; }
    public string DocumentProcess { get; set; }
}
