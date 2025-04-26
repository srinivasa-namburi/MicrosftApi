using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events
{
    /// <summary>
    /// Represents a content chunk update notification.
    /// </summary>
    public class ContentChunkUpdate
    {
        /// <summary>
        /// Unique identifier for the update.
        /// </summary>
        public Guid UpdateId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The conversation ID associated with this update.
        /// </summary>
        public Guid ConversationId { get; set; }

        /// <summary>
        /// The message ID that triggered this update.
        /// </summary>
        public Guid MessageId { get; set; }

        /// <summary>
        /// List of content chunks to be applied.
        /// </summary>
        public List<ContentChunk> Chunks { get; set; } = new List<ContentChunk>();

        /// <summary>
        /// Indicates if this is the final update in the sequence.
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Optional status message associated with the update.
        /// </summary>
        public string StatusMessage { get; set; }
    }

    /// <summary>
    /// Represents a single chunk of content to be updated.
    /// </summary>
    public class ContentChunk
    {
        /// <summary>
        /// The original text segment being replaced.
        /// </summary>
        public string OriginalText { get; set; }

        /// <summary>
        /// The new text to replace the original.
        /// </summary>
        public string NewText { get; set; }

        /// <summary>
        /// Start position in the original text.
        /// </summary>
        public int StartPosition { get; set; }

        /// <summary>
        /// End position in the original text.
        /// </summary>
        public int EndPosition { get; set; }

        /// <summary>
        /// Type of update operation.
        /// </summary>
        public ContentChunkType ChunkType { get; set; }

        /// <summary>
        /// Context for locating the chunk (text before/after) for additional validation.
        /// </summary>
        public string Context { get; set; }
    }

    /// <summary>
    /// Types of content chunk operations.
    /// </summary>
    public enum ContentChunkType
    {
        /// <summary>
        /// Replace existing content.
        /// </summary>
        Replace,
        
        /// <summary>
        /// Insert new content.
        /// </summary>
        Insert,
        
        /// <summary>
        /// Delete existing content.
        /// </summary>
        Delete
    }
}
