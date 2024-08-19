using MassTransit.Futures.Contracts;

namespace ProjectVico.V2.DocumentProcess.Shared.Prompts;

public interface IPromptCatalogTypes
{
    string ChatSystemPrompt { get; }
    string ChatSinglePassUserPrompt { get; }
    
}