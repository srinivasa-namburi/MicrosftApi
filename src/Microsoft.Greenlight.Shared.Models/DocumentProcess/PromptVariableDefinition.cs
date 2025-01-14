using Microsoft.Greenlight.Shared.Models.DocumentProcess;
using Microsoft.Greenlight.Shared.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a definition for a variable used in a prompt.
/// </summary>
public class PromptVariableDefinition : EntityBase
{
    /// <summary>
    /// Unique ID of the associated prompt definition.
    /// </summary>
    public required Guid PromptDefinitionId { get; set; }

    /// <summary>
    /// Prompt definition associated with the variable.
    /// </summary>
    [JsonIgnore]
    public PromptDefinition? PromptDefinition { get; set; }

    /// <summary>
    /// Name of the variable
    /// </summary>
    public required string VariableName { get; set; }

    /// <summary>
    /// Description of the variable.
    /// </summary>
    public string? Description { get; set; }
}