namespace ProjectVico.V2.Shared.Prompts;

public interface IPromptCatalogTypes
{
    string ChatSystemPrompt { get; }
    string ChatSinglePassUserPrompt { get; }

    string SectionGenerationMainPrompt { get; }
    string SectionGenerationSummaryPrompt { get; }
    string SectionGenerationMultiPassContinuationPrompt { get; }
    string SectionGenerationSystemPrompt { get; }
}



