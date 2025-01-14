using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

/// <summary>
/// Command to generate a report title section.
/// </summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record GenerateReportTitleSection(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// JSON representation of the content node.
    /// </summary>
    public string ContentNodeJson { get; set; }

    /// <summary>
    /// JSON representation of the document outline.
    /// </summary>
    public string DocumentOutlineJson { get; set; }

    /// <summary>
    /// OID of the author.
    /// </summary>
    public string AuthorOid { get; set; }

    /// <summary>
    /// Optional metadata ID.
    /// </summary>
    public Guid? MetadataId { get; set; }
}
