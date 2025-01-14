using System.Text.Json.Serialization;

namespace Microsoft.Greenlight.Shared.Models.DocumentProcess;

/// <summary>
/// Represents the implementation of a prompt within a document process.
/// </summary>
public class PromptImplementation : EntityBase
{
    /// <summary>
    /// Unique identifier for the prompt definition.
    /// </summary>
    public required Guid PromptDefinitionId { get; set; }

    /// <summary>
    /// Prompt definition associated with this implementation.
    /// </summary>
    [JsonIgnore]
    public PromptDefinition? PromptDefinition { get; set; }

    /// <summary>
    /// Unique identifier for the document process definition.
    /// </summary>
    public required Guid? DocumentProcessDefinitionId { get; set; }

    /// <summary>
    /// Document process definition associated with this implementation.
    /// </summary>
    [JsonIgnore]
    public DynamicDocumentProcessDefinition? DocumentProcessDefinition { get; set; }

    /// <summary>
    /// Short code for the static document process.
    /// </summary>
    public string? StaticDocumentProcessShortCode { get; set; }

    /// <summary>
    /// Text of the prompt.
    /// </summary>
    public required string Text { get; set; }
}
