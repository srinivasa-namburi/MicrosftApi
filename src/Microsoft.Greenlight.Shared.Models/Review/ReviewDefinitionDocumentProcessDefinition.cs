using System.Text.Json.Serialization;
using Microsoft.Greenlight.Shared.Models.DocumentProcess;

namespace Microsoft.Greenlight.Shared.Models.Review;

public class ReviewDefinitionDocumentProcessDefinition : EntityBase
{
    public required Guid ReviewId { get; set; }
    [JsonIgnore]
    public ReviewDefinition? Review { get; set; }

    public required Guid DocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    public bool IsActive { get; set; } = true;

}
