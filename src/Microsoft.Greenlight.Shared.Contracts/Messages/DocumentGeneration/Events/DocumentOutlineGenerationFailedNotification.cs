using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Events
{
    public record DocumentOutlineGenerationFailedNotification(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        /// <summary>
        /// The OID of the author who generated the document.
        /// </summary>
        public string? AuthorOid { get; set; }
    }
}