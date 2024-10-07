using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

public class PromptDefinition : EntityBase
{
    public required string ShortCode { get; set; }
    public string? Description { get; set; }

    public List<PromptImplementation> Implementations { get; set; } = [];

    public List<PromptVariableDefinition> Variables { get; set; } = [];
}

public class PromptVariableDefinition : EntityBase
{
    public required Guid PromptDefinitionId { get; set; }
    [JsonIgnore]
    public PromptDefinition? PromptDefinition { get; set; }
    public required string VariableName { get; set; }
    public string? Description { get; set; }
}
