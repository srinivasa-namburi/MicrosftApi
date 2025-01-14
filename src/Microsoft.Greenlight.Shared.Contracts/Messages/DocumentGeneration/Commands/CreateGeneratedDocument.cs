using MassTransit;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.DocumentGeneration.Commands;

/// <summary>Command to create a generated document.</summary>
/// <param name="CorrelationId">The correlation ID of the command.</param>
public record CreateGeneratedDocument(Guid CorrelationId) : CorrelatedBy<Guid>
{
    /// <summary>
    /// Original DTO for document generation.
    /// </summary>
    public required GenerateDocumentDTO OriginalDTO { get; set; }
}
