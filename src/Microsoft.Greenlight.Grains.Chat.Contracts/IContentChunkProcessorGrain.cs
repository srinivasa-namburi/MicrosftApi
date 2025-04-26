using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts
{
    /// <summary>
    /// Interface for content chunk processor grain
    /// </summary>
    public interface IContentChunkProcessorGrain : IGrainWithGuidKey
    {
        /// <summary>
        /// Process a content update request in chunk mode
        /// </summary>
        /// <param name="conversationId">The conversation ID</param>
        /// <param name="messageId">The message ID</param>
        /// <param name="originalContent">Original content to be updated</param>
        /// <param name="userQuery">The user's query/instructions</param>
        /// <param name="documentProcessName">The document process name</param>
        /// <param name="systemPrompt">System prompt to use</param>
        /// <returns>A task representing the asynchronous operation</returns>
        Task ProcessContentUpdateAsync(
            Guid conversationId, 
            Guid messageId,
            string originalContent, 
            string userQuery, 
            string documentProcessName,
            string systemPrompt);
    }
}