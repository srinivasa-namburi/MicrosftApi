using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Models.Review;

public class ReviewDefinition : EntityBase
{
    public List<ReviewQuestion> ReviewQuestions { get; set; } = new();
    public required string Title { get; set; }
    public string? Description { get; set; }

    public List<ReviewDefinitionDocumentProcessDefinition> DocumentProcessDefinitionConnections { get; set; } = new();

    public List<ReviewInstance> ReviewInstances { get; set; } = new();
}
