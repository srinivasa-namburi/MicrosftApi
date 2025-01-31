namespace Microsoft.Greenlight.Shared.Prompts;

/// <summary>
/// Interface for defining various prompt types used in the system.
/// </summary>
public interface IPromptCatalogTypes
{
    /// <summary>
    /// Gets the system prompt for chat.
    /// </summary>
    string ChatSystemPrompt { get; }

    /// <summary>
    /// Gets the single pass user prompt for chat.
    /// </summary>
    string ChatSinglePassUserPrompt { get; }

    /// <summary>
    /// Gets the main prompt for section generation.
    /// </summary>
    string SectionGenerationMainPrompt { get; }

    /// <summary>
    /// Gets the summary prompt for section generation.
    /// </summary>
    string SectionGenerationSummaryPrompt { get; }

    /// <summary>
    /// Gets the multi-pass continuation prompt for section generation.
    /// </summary>
    string SectionGenerationMultiPassContinuationPrompt { get; }

    /// <summary>
    /// Gets the system prompt for section generation.
    /// </summary>
    string SectionGenerationSystemPrompt { get; }
}