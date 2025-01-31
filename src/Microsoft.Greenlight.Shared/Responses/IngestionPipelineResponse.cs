using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Responses;

/// <summary>
/// Represents the response of the ingestion pipeline.
/// </summary>
public class IngestionPipelineResponse
{
    /// <summary>
    /// Gets or sets the list of tables.
    /// </summary>
    public List<Table>? Tables { get; set; }

    /// <summary>
    /// Gets or sets the list of content nodes.
    /// </summary>
    public List<ContentNode> ContentNodes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the ingestion was successful.
    /// </summary>
    public bool IsSuccessful { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether there is unsupported classification.
    /// </summary>
    public bool UnsupportedClassification { get; set; } = false;
}
