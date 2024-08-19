using Microsoft.Extensions.DependencyInjection;
using ProjectVico.V2.DocumentProcess.Shared.Prompts;

namespace ProjectVico.V2.DocumentProcess.US.NRC.EnvironmentalReport.Prompts;

public class USNRCEnvironmentalReportPromptCatalogTypes : IPromptCatalogTypes
{
    private readonly IPromptCatalogTypes _defaultPromptCatalogTypes;

    public USNRCEnvironmentalReportPromptCatalogTypes(
        [FromKeyedServices("Default-IPromptCatalogTypes")]
        IPromptCatalogTypes defaultPromptCatalogTypes
        )
    {
        _defaultPromptCatalogTypes = defaultPromptCatalogTypes;
    }

    public string ChatSystemPrompt =>
        """
        This is a chat between an intelligent AI bot specializing in assisting with producing 
        NRC environmental reports - and one or more human participants. 
        
        You are free to answer other queries as well, but this is your primary purpose.
        
        The AI has been trained on GPT-4 LLM data through to October 2023 and has access 
        to an additional repository of data by using the "native_KmDocs" plugin and its various methods. 
        
        The "native_KmDocs" repository contains a variety of documents, including NRC environmental reports.
        
        Try to be complete with your responses. Please - no polite endings like 'i hope that helps', no beginning with 
        'Sure, I can do that', etc.
        """;
    public string ChatSinglePassUserPrompt => _defaultPromptCatalogTypes.ChatSinglePassUserPrompt;
}