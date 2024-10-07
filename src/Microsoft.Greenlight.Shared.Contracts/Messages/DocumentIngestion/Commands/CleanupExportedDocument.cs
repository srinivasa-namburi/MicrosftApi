using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands
{
    public record CleanupExportedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        public Guid ExportedDocumentLinkId { get; set; }

        public string BlobContainer { get; set; }

        public string FileName { get; set; }
    }
}
