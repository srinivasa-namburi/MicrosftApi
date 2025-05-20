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

    /// <summary>
    /// Gets the prompt used to answer review questions - in question format.
    /// </summary>
    string ReviewQuestionAnswerPrompt { get; }

    /// <summary>
    /// Gets the prompt used to answer review questions - in requirement format.
    /// </summary>
    string ReviewRequirementAnswerPrompt { get; }

    /// <summary>
    /// Gets the prompt used to provide a reasoning for the review sentiment.
    /// </summary>
    string ReviewSentimentReasoningPrompt { get; }

    /// <summary>
    /// Gets the prompt used to provide a sentiment score for the review answer.
    /// </summary>
    string ReviewSentimentAnalysisScorePrompt { get; }

    /// <inheritdoc />
    string SectionGenerationAgenticMainPrompt { get; }
}