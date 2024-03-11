using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.Shared.Responses;

public class IngestionPipelineResponse
{
    public List<Table>? Tables { get; set; }
    public List<ContentNode> ContentNodes { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public bool UnsupportedClassification { get; set; } = false;
}