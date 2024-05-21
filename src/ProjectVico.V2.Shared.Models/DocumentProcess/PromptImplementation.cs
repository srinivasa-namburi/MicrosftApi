using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models.DocumentProcess;

public class PromptDefinition : EntityBase
{
    public required string ShortCode { get; set; }
    public string? Description { get; set; }

    public List<PromptImplementation> Implementations { get; set; } = [];
}

public class PromptImplementation : EntityBase
{
    public required Guid PromptDefinitionId { get; set; }
    [JsonIgnore]
    public PromptDefinition? PromptDefinition { get; set; }

    public required Guid DocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }


    public required string Text { get; set; }

}