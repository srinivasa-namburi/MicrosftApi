namespace Microsoft.Greenlight.Shared.Contracts.Components
{
    /// <summary>
    /// Inline component of both models and deployments that defines the max token limits for different types of content generation.
    /// </summary>
    public class AiModelMaxTokenSettings
    {
        /// <summary>
        /// Maximum tokens allowed for content generation.
        /// </summary>
        public int MaxTokensForContentGeneration { get; set; }
        
        /// <summary>
        /// Maximum tokens allowed for summarization.
        /// </summary>
        public int MaxTokensForSummarization { get; set; }
        
        /// <summary>
        /// Maximum tokens allowed for validation.
        /// </summary>
        public int MaxTokensForValidation { get; set; }
        
        /// <summary>
        /// Maximum tokens allowed for chat replies.
        /// </summary>
        public int MaxTokensForChatReplies { get; set; }
        
        /// <summary>
        /// Maximum tokens allowed for each question/answer pair in review executions
        /// </summary>
        public int MaxTokensForQuestionAnswering { get; set; }
        
        /// <summary>
        /// Max tokens allowed for general use. Mostly for short responses, recommend keeping this low for performance
        /// reasons. (approx 1/16th of model capability or less).
        /// </summary>
        public int MaxTokensGeneral { get; set; }
    }
}