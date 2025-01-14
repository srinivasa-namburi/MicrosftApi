namespace Microsoft.Greenlight.Shared.Contracts.DTO;

/// <summary>
/// Represents information about a prompt variable.
/// </summary>
public record PromptVariableInfo
{
    /// <summary>
    /// Unique identifier of the prompt variable.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier of the prompt definition.
    /// </summary>
    public Guid PromptDefinitionId { get; set; }

    /// <summary>
    /// Name of the variable.
    /// </summary>
    public required string VariableName { get; set; }

    /// <summary>
    /// Description of the prompt variable.
    /// </summary>
    public string? Description { get; set; }
}
