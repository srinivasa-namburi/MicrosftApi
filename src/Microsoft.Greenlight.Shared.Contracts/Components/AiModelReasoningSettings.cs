using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Components
{
    /// <summary>
    /// Reasoning settings for different types of AI model tasks.
    /// Defaults to medium reasoning level for all tasks.
    /// </summary>
    public class AiModelReasoningSettings
    {
        /// <summary>
        /// Reasoning level for content generation.
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelForContentGeneration { get; set; } = AiModelReasoningLevel.Medium;

        /// <summary>
        /// Reasoning level for summarization.
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelForSummarization { get; set; } = AiModelReasoningLevel.Medium;

        /// <summary>
        /// Reasoning level for validation.
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelForValidation { get; set; } = AiModelReasoningLevel.Medium;

        /// <summary>
        /// Reasoning level for chat messages
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelForChatReplies { get; set; } = AiModelReasoningLevel.Medium;

        /// <summary>
        /// Reasoning level for review question answering.
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelForQuestionAnswering { get; set; } = AiModelReasoningLevel.Medium;

        /// <summary>
        /// Reasoning level for general use. Mostly for short responses, recommend keeping this low for performance
        /// </summary>
        public AiModelReasoningLevel ReasoningLevelGeneral { get; set; } = AiModelReasoningLevel.Medium;
    }
}