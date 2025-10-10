namespace Microsoft.Greenlight.Shared.Prompts;

/// <summary>
/// Contains constant string values for various prompt names used in the application.
/// </summary>
public static class PromptNames
{
    /// <summary>
    /// The prompt name for the chat system.
    /// </summary>
    public const string ChatSystemPrompt = "ChatSystemPrompt";

    /// <summary>
    /// The prompt name for a single pass user chat.
    /// </summary>
    public const string ChatSinglePassUserPrompt = "ChatSinglePassUserPrompt";

    /// <summary>
    /// The prompt name for the main section generation.
    /// </summary>
    public const string SectionGenerationMainPrompt = "SectionGenerationMainPrompt";

    /// <summary>
    /// The prompt name for the section generation summary.
    /// </summary>
    public const string SectionGenerationSummaryPrompt = "SectionGenerationSummaryPrompt";

    /// <summary>
    /// The prompt name for the continuation of multi-pass section generation.
    /// </summary>
    public const string SectionGenerationMultiPassContinuationPrompt = "SectionGenerationMultiPassContinuationPrompt";

    /// <summary>
    /// The prompt name for the section generation system.
    /// </summary>
    public const string SectionGenerationSystemPrompt = "SectionGenerationSystemPrompt";

    /// <summary>
    /// Gets the prompt used to answer review questions - in question format.
    /// </summary>
    public const string ReviewQuestionAnswerPrompt = "ReviewQuestionAnswerPrompt";

    /// <summary>
    /// Gets the prompt used to answer review questions - in requirement format.
    /// </summary>
    public const string ReviewRequirementAnswerPrompt = "ReviewRequirementAnswerPrompt";

    /// <summary>
    /// Gets the prompt used to provide a reasoning for the review sentiment.
    /// </summary>
    public const string ReviewSentimentReasoningPrompt = "ReviewSentimentReasoningPrompt";

    /// <summary>
    /// Gets the prompt used to provide a sentiment score for the review answer.
    /// </summary>
    public const string ReviewSentimentAnalysisScorePrompt = "ReviewSentimentAnalysisScorePrompt";

    /// <summary>
    /// Gets the prompt used to generate a section with an agentic approach (ContentWriter Agent).
    /// </summary>
    public const string SectionGenerationAgenticMainPrompt = "SectionGenerationAgenticMainPrompt";

    // System-wide prompts (Flow AI Assistant)
    /// <summary>
    /// System prompt for Flow backend conversations (conversations between Flow and document processes).
    /// </summary>
    public const string FlowBackendConversationSystemPrompt = "FlowBackendConversationSystemPrompt";

    /// <summary>
    /// System prompt for Flow user conversations (direct conversations with users).
    /// </summary>
    public const string FlowUserConversationSystemPrompt = "FlowUserConversationSystemPrompt";

    /// <summary>
    /// Prompt for Flow intent detection (determines which document processes to engage).
    /// </summary>
    public const string FlowIntentDetectionPrompt = "FlowIntentDetectionPrompt";

    /// <summary>
    /// Prompt for Flow response synthesis (combines responses from multiple document processes).
    /// </summary>
    public const string FlowResponseSynthesisPrompt = "FlowResponseSynthesisPrompt";

    /// <summary>
    /// Prompt for Flow conversational fallback (used when no specific document process intent is detected).
    /// </summary>
    public const string FlowConversationalFallbackPrompt = "FlowConversationalFallbackPrompt";
}
