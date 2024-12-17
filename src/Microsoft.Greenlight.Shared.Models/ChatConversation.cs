namespace Microsoft.Greenlight.Shared.Models;

public class ChatConversation : EntityBase
{
    public string DocumentProcessName { get; set; } = "US.NuclearLicensing";

    public string SystemPrompt { get; set; } = """
                                               This is a chat between an intelligent AI bot specializing in assisting with producing 
                                               environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more 
                                               participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access 
                                               to additional data on more recent SMR environmental report samples. Try to be complete 
                                               with your responses. Provide responses that can be copied directly into an 
                                               environmental report, so no polite endings like 'i hope that helps', no beginning with 
                                               'Sure, I can do that', etc.
                                               """;

    public List<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
