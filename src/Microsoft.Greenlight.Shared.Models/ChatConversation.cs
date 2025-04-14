using Orleans;

namespace Microsoft.Greenlight.Shared.Models;

/// <summary>
/// Represents a chat conversation, including the document process name, system prompt, and chat messages.
/// </summary>
[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ChatConversation : EntityBase
{
    /// <summary>
    /// Name of the document process.
    /// </summary>
    public string DocumentProcessName { get; set; } = "US.NuclearLicensing";

    /// <summary>
    /// System prompt for the chat conversation.
    /// </summary>
    public string SystemPrompt { get; set; } = """
                                               This is a chat between an intelligent AI bot specializing in assisting with producing 
                                               environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more 
                                               participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access 
                                               to additional data on more recent SMR environmental report samples. Try to be complete 
                                               with your responses. Provide responses that can be copied directly into an 
                                               environmental report, so no polite endings like 'i hope that helps', no beginning with 
                                               'Sure, I can do that', etc.
                                               """;

    /// <summary>
    /// List of chat messages in the conversation.
    /// </summary>
    public List<ChatMessage> ChatMessages { get; set; } = [];

    /// <summary>
    /// List of reference item IDs in the conversation.
    /// </summary>
    public List<Guid> ReferenceItemIds { get; set; } = new List<Guid>();
}
