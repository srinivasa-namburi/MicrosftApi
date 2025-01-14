using MassTransit;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

/// <summary>Command to generate a document outline.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record GenerateDocumentOutline(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Title of the document.
    /// </summary>
    public string DocumentTitle { get; set; }

    /// <summary>
    /// OID of the author.
    /// </summary>
    public string? AuthorOid { get; set; }

    /// <summary>
    /// Document process to use.
    /// </summary>
    public string? DocumentProcess { get; set; }
}
