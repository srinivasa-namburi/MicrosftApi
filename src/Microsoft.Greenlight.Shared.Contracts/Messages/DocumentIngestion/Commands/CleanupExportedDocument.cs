using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentIngestion.Commands
{
    /// <summary>
    /// Command to cleanup an exported document.
    /// </summary>
    /// <param name="CorrelationId">The correlation ID of the command.</param>
    public record CleanupExportedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        /// <summary>
        /// ID of the exported document link.
        /// </summary>
        public Guid ExportedDocumentLinkId { get; set; }

        /// <summary>
        /// Name of the blob container.
        /// </summary>
        public string? BlobContainer { get; set; }

        /// <summary>
        /// Name of the file.
        /// </summary>
        public string? FileName { get; set; }
    }
}
