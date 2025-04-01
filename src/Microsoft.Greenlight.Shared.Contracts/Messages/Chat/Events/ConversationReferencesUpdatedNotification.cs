using System;
using System.Collections.Generic;
using Microsoft.Greenlight.Shared.Contracts.DTO;

namespace Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events
{
    /// <summary>
    /// Event raised when conversation references are updated.
    /// </summary>
    public record ConversationReferencesUpdatedNotification
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConversationReferencesUpdatedNotification"/> class.
        /// </summary>
        /// <param name="conversationId">The conversation ID.</param>
        /// <param name="referenceItems">The list of reference items.</param>
        public ConversationReferencesUpdatedNotification(Guid conversationId, List<ContentReferenceItemInfo> referenceItems)
        {
            ConversationId = conversationId;
            ReferenceItems = referenceItems;
        }

        /// <summary>
        /// Gets or sets the conversation ID.
        /// </summary>
        public Guid ConversationId { get; set; }
        
        /// <summary>
        /// Gets or sets the list of content reference items for the conversation.
        /// </summary>
        public List<ContentReferenceItemInfo> ReferenceItems { get; set; } = [];
    }
}