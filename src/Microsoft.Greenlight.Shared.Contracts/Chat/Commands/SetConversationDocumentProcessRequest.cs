// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.Chat.Commands;

/// <summary>
/// Request to set or change the document process for a chat conversation.
/// </summary>
public class SetConversationDocumentProcessRequest
{
    /// <summary>
    /// The document process short name to set on the conversation.
    /// </summary>
    public string DocumentProcessName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to update the conversation's system prompt to the default for the new document process.
    /// </summary>
    public bool UpdateSystemPrompt { get; set; } = true;
}

