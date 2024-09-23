using System.Text.Json.Serialization;

namespace ProjectVico.V2.Shared.Models.DocumentProcess;

public class PromptImplementation : EntityBase
{
    public required Guid PromptDefinitionId { get; set; }
    [JsonIgnore]
    public PromptDefinition? PromptDefinition { get; set; }

    public required Guid? DocumentProcessDefinitionId { get; set; }
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }
    public string? StaticDocumentProcessShortCode { get; set; }


    public required string Text { get; set; }

}