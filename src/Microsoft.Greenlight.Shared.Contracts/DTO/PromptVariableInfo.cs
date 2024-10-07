namespace Microsoft.Greenlight.Shared.Contracts.DTO;

public record PromptVariableInfo
{
    public Guid Id { get; set; }
    public Guid PromptDefinitionId { get; set; }
    public required string VariableName { get; set; }
    public string? Description { get; set; }
}
