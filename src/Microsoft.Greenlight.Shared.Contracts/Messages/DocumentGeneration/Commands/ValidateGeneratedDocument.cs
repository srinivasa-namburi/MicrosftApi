using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands
{
    /// <summary>
    /// Command to validate report content after generation has finished.
    /// </summary>
    /// <param name="CorrelationId">The correlation ID, which in this case is the GeneratedDocument ID.</param>
    public record ValidateGeneratedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
    {

    }

    /// <summary>
    /// Validate the contents of a single ContentNode (Title or Heading) against the full document text.
    /// </summary>
    /// <param name="CorrelationId">ID of the associated document</param>
    public record ValidateReportContentNode(Guid CorrelationId) : CorrelatedBy<Guid>
    {
        /// <summary>
        /// ID of the ContentNode (Title or Heading) to validate against the full document text
        /// </summary>
        public required Guid ContentNodeId { get; set; }

        /// <summary>
        /// Full text of the generated document that this ContentNode is a part of
        /// </summary>
        public required string FullDocumentText { get; set; }

    }
}