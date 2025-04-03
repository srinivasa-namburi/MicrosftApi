namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events
{
    /// <summary>
    /// Notification for chat message processing status updates
    /// </summary>
    public record ChatMessageStatusNotification
    {
        /// <summary>
        /// The ID of the chat message this status update relates to
        /// </summary>
        public Guid ChatMessageId { get; init; }
        
        /// <summary>
        /// The status message to display
        /// </summary>
        public string StatusMessage { get; init; }
        
        /// <summary>
        /// Whether processing is complete (determines whether to show spinner)
        /// </summary>
        public bool ProcessingComplete { get; init; }
        
        /// <summary>
        /// Whether the notification should persist until a new message is received
        /// </summary>
        public bool Persistent { get; init; }
        
        /// <summary>
        /// Creates a new chat message status notification
        /// </summary>
        public ChatMessageStatusNotification(Guid chatMessageId, string statusMessage, bool processingComplete = false, bool persistent = false)
        {
            ChatMessageId = chatMessageId;
            StatusMessage = statusMessage;
            ProcessingComplete = processingComplete;
            Persistent = persistent;
        }
    }
}