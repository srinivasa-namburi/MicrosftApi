using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events
{
    /// <summary>
    /// Event raised when a document outline is generated - used for notifications
    /// </summary>

    public record DocumentOutlineGeneratedNotification(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        /// <summary>
        /// The OID of the author who generated the document.
        /// </summary>
        public string? AuthorOid { get; set; }
    }
}