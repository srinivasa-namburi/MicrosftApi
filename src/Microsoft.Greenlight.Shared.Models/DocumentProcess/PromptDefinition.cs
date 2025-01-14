namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

/// <summary>
/// Represents a definition for a prompt, including its shortcode, description, implementations, and variables.
/// </summary>
public class PromptDefinition : EntityBase
{
    /// <summary>
    /// Shortcode for the prompt.
    /// </summary>
    public required string ShortCode { get; set; }

    /// <summary>
    /// Description of the prompt.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of implementations for the prompt.
    /// </summary>
    public List<PromptImplementation> Implementations { get; set; } = [];

    /// <summary>
    /// List of variable definitions for the prompt.
    /// </summary>
    public List<PromptVariableDefinition> Variables { get; set; } = [];
}