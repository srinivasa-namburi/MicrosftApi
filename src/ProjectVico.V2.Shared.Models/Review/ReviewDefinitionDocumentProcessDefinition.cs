using System.Text.Json.Serialization;
using ProjectVico.V2.Shared.Models.DocumentProcess;

namespace ProjectVico.V2.Shared.Models.Review;

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