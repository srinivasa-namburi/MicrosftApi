using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Responses;

public class IngestionPipelineResponse
{
    public List<Table>? Tables { get; set; }
    public List<ContentNode> ContentNodes { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public bool UnsupportedClassification { get; set; } = false;
}
