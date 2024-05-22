using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProjectVico.V2.DocumentProcess.Shared.Generation;
using ProjectVico.V2.DocumentProcess.Shared.Plugins.KmDocs;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Models;

namespace ProjectVico.V2.DocumentProcess.Belgium.NuclearLicensing.DSAR.Generation;

public class BelgiumNuclearLicensingDSARAiCompletionService : IAiCompletionService
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly OpenAIClient _openAIClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<BelgiumNuclearLicensingDSARAiCompletionService> _logger;
    private readonly IServiceProvider _sp;
    private Kernel _sk;
    private readonly int _numberOfPasses = 6;

    public BelgiumNuclearLicensingDSARAiCompletionService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
        DocGenerationDbContext dbContext,
        IServiceProvider sp, 
        ILogger<BelgiumNuclearLicensingDSARAiCompletionService> logger)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _openAIClient = openAIClient;
        _dbContext = dbContext;
        _sp = sp;
        _logger = logger;
    }

    public async Task<List<ContentNode>> GetBodyContentNodes(List<ReportDocument> documents,
        string sectionOrTitleNumber, string sectionOrTitleText, ContentNodeType contentNodeType,
        string tableOfContentsString, Guid? metadataId)
    {
        var combinedBodyTextStringBuilder = new StringBuilder();

        await foreach (var bodyContentNodeString in GetStreamingBodyContentText(documents, sectionOrTitleNumber, sectionOrTitleText,
                           contentNodeType, tableOfContentsString, metadataId))
        {
            combinedBodyTextStringBuilder.Append(bodyContentNodeString);
        }

        var combinedBodyText = combinedBodyTextStringBuilder.ToString();

        var combinedBodyTextNode = new ContentNode()
        {
            Id = Guid.NewGuid(),
            Text = combinedBodyText,
            Type = ContentNodeType.BodyText,
            GenerationState = ContentNodeGenerationState.Completed
        };

        return [combinedBodyTextNode];
    }

    public async IAsyncEnumerable<string> GetStreamingBodyContentText(List<ReportDocument> documents,
        string sectionOrTitleNumber, string sectionOrTitleText,
        ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId)
    {
        var plugins = new KernelPluginCollection();
        var documentProcess = _serviceConfigurationOptions.ProjectVicoServices.DocumentProcesses.Single(x => x.Name == "Belgium.NuclearLicensing.DSAR");
        
        plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(_sp, documentProcess!,
            excludedPluginTypes: [typeof(KmDocsPlugin)]);
                    
        _sk = new Kernel(_sp, plugins);

        var sectionExample = new StringBuilder();
        var firstDocuments = documents.Take(15).ToList();
        
        foreach (var document in firstDocuments)
        {
            sectionExample.AppendLine($"[EXAMPLE: Document Extract]");
            sectionExample.AppendLine(document.Content);
            sectionExample.AppendLine();
        }

        var exampleString = sectionExample.ToString();
        string originalPrompt = "";
        var chatResponses = new List<string>();
        var systemPrompt = """
                           [SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing 
                           french language DSAR reports for Small Modular nuclear Reactors ('SMR') and one or more participants. 
                           The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent 
                           output samples from previous French and Belgian DSAR reports. These samples are in French language. 
                           Provide responses that can be copied directly into an environmental report. Provide your output in French language.
                           """;

        var lastPassResponse = new List<string>();

        var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);

        string customDataString = "No custom data available for this query";

        if (documentMetaData != null && !string.IsNullOrEmpty(documentMetaData.MetadataJson))
        {
            customDataString = documentMetaData.MetadataJson;
        }

        string fullSectionName = "";

        if (string.IsNullOrEmpty(sectionOrTitleNumber))
        {
            fullSectionName = sectionOrTitleText;
        }
        else
        {
            fullSectionName = $"{sectionOrTitleNumber} {sectionOrTitleText}";
        }

        for (int i = 0; i < _numberOfPasses; i++)
        {
            string prompt;
            if (i == 0)
            {
                prompt = $$"""

                           OUTPUT SHOULD BE IN FRENCH LANGUAGE.
                           
                           This is the initial query in a multi-pass conversation. You are not expected to return the full output in this pass.
                           However, please be as complete as possible in your response for this pass. For this task, including this initial query,
                           we will be performing {{_numberOfPasses}} passes to form a complete response. This is the first pass.

                           There will be additional queries asking to to expand on a combined summary of the output you provide here and
                           further summaries from later responses.

                           You are writing the section {{fullSectionName}}. The section examples may contain input from
                           additional sub-sections in addition to the specific section you are writing.
                           
                           Below, there are several extracts/fragments from previous applications denoted by [EXAMPLE: Document Extract].

                           Using this information, write a similar section(sub-section) or chapter(section),
                           depending on which is most appropriate to the query. The fragments might contain information from different sections, 
                           not just the one you are writing ({{fullSectionName}}). Filter out any irrelevant information and write a coherent section.

                           For customizing the output so that it pertains to this project, please use tool calling/functions as supplied to you
                           in the list of available functions. If you need additional data, please request it using the [DETAIL: <dataType>] tag.

                           Custom data for this project follows in JSON format between the [CUSTOMDATA] and [/CUSTOMDATA] tags. IGNORE the following fields:
                           DocumentProcessName, MetadataModelName, DocumentGenerationRequestFullTypeName, ID, AuthorOid.

                           [CUSTOMDATA]
                           {{customDataString}}
                           [/CUSTOMDATA]
                           
                           In between the [TOC] and [/TOC] tags below, you will find a table of contents for the entire document.
                           Please make sure to use this table of contents to ensure that the section you are writing fits in with the rest of the document,
                           and to avoid duplicating content that is already present in the document. Pay particular attention to neighboring sections and the
                           parent title of the section you're writing. If you see references to sections in the section you're writing,
                           please use this TOC to validate chapter and section numbers and to ensure that the references are correct. Please don't
                           refer to tables or sections that are not in the TOC provided here.

                           [TOC]
                           {{tableOfContentsString.TrimEnd()}}
                           [/TOC]

                           Be as verbose as necessary to include all required information. Try to be very complete in your response, considering all source data. We
                           are looking for full sections or chapters - not short summaries.

                           For headings, remove the numbering and any heading prefixes like "Section" or "Chapter" from the heading text.

                           Make sure your output complies with UTF-8 encoding. Paragraphs should be separated by two newlines (\n\n)
                           For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.

                           Format the text with Markdown syntax. For example, use #, ##, ### for headings, * and - for bullet points, etc.

                           For table outputs, look at the example content and look for tables that match the table description. Please render them inline or at the end
                           of the text as Markdown Tables. If they are too complex, you can use HTML tables instead that accomodate things like rowspan and colspan.
                           You should adopt the contents of these tables to fit any custom inputs you have received regarding location, size, reactor specifics
                           and so on. If you have no such inputs, consider the tables as they are in the examples as the default.

                           If you are missing details to write specific portions of the text, please indicate that with [DETAIL: <dataType>] -
                           and put the type of data needed in the dataType parameter. Make sure to look at all the source content before you decide you lack details!

                           Be concise in these DETAIL requests - no long sentences, only data types with a one or two word description of what's missing.

                           If you believe the output is complete (and this is the last needed pass to complete the whole section), please end your response with the following text on a
                           new line by itself:
                           [*COMPLETE*]

                           {{exampleString}}

                           """;
                originalPrompt = prompt;
            }
            else
            {
                var summary = await SummarizeOutput(string.Join("\n\n", chatResponses));
                prompt = $"""
                           This is a continuing conversation.
                           
                           You're now going to continue the previous conversation, expanding on the previous output.
                           A summary of the output up to now is here :
                           {summary}
                           
                           As a reminder, this was the output of the last pass - DO NOT REPEAT THIS INFORMATION IN THIS PASS:
                           {string.Join("\n\n", lastPassResponse)}
                           
                           For the next step, you should continue the conversation. Here's the prompt you should use - but
                           take care not to repeat the same information you've already provided (as detailed in the summary). Also ignore the initial 
                           heading and first two paragraphs of the prompt detailing how to respond to the first pass query. 
                           This is pass number {i + 1} of {_numberOfPasses} - take that into account when you respond.
                           
                           Please start your response with content only - no ASSISTANT texts explaining your logic, tasks or reasoning.
                           The output from the passes should be possible to tie together with no further parsing necessary.
                           
                           If you believe the output for the whole section is complete (and this is the last needed pass),
                           please end your response with the following text on a
                           new line by itself:
                           [*COMPLETE*]
                           
                           Note - do NOT use that tag to delineate the end of a single response. It should only be used to indicate the end of the whole section
                           when no more passes are needed to finish the section output.
                           
                           ORIGINAL PROMPT with examples:
                           {originalPrompt}
                          """;
            }

            string responseLine = "";
            await foreach (var stringUpdate in ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(systemPrompt,
                               prompt))
            {
                Console.Write(stringUpdate);
                // Continue building the update until we reach a new line
                responseLine += stringUpdate;

                // If the response contains the [*COMPLETE*] tag, we can stop the conversation
                if (responseLine.Contains("[*COMPLETE*]", StringComparison.InvariantCultureIgnoreCase))
                {
                    yield break;
                }

                // If the response doesn't contain the [*TO BE CONTINUED*] tag, we can add it to the last response in the list
                if (responseLine
                    .Contains("[*TO BE CONTINUED*]", StringComparison.InvariantCultureIgnoreCase))
                {
                    responseLine = responseLine.Replace("[*TO BE CONTINUED*]", "",
                        StringComparison.InvariantCultureIgnoreCase);
                }

                if (responseLine.Contains("\n"))
                {
                    chatResponses.Add(responseLine);
                    yield return responseLine;
                    responseLine = "";
                }
            }
        }
    }

    private async IAsyncEnumerable<string> ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(string systemPrompt,
             string userPrompt)
    {
        var openAiExecutionSettings = new OpenAIPromptExecutionSettings
        {
            ChatSystemPrompt = systemPrompt,
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            MaxTokens = 4000,
            Temperature = 0.4f,
            FrequencyPenalty = 0.5f
        };

        await foreach (var update in _sk.InvokePromptStreamingAsync(userPrompt, new KernelArguments(openAiExecutionSettings)))
        {
            yield return update.ToString();
        }
    }

    private async Task<string> SummarizeOutput(string originalContent)
    {
        // using a streaming OpenAi ChatCompletion, summarize the originalContent with up to 8000 tokens and return the summary
        var systemPrompt = """
                           [SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing
                           french language DSAR reports for Small Modular nuclear Reactors ('SMR') and one or more participants.
                           The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent
                           output samples from previous French and Belgian DSAR reports. These samples are in French language.
                           Provide responses that can be copied directly into an environmental report. Provide your output in French language.
                           """;

        var summarizePrompt = $"""
                              When responding, do not include ASSISTANT: or initial greetings/information about the reply. 
                              Only the content/summary, please. Please summarize the following text (use French language) so it can form the basis of further 
                              expansion: 
                              
                              {originalContent}
                              """;

        var chatResponses = new List<string>();

        var chatCompletionOptions = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(summarizePrompt)
            },
            DeploymentName = _serviceConfigurationOptions.OpenAi.Gpt4o_Or_Gpt4128KDeploymentName,
            MaxTokens = 4000,
            Temperature = 0.5f,
            FrequencyPenalty = 0.5f
        };

        StringBuilder chatStringBuilder = new StringBuilder();
        Console.WriteLine("********************************");
        Console.WriteLine("SUMMARY SO FAR:");
        Console.WriteLine("********************************");
        await foreach (StreamingChatCompletionsUpdate chatUpdate in
                       await _openAIClient.GetChatCompletionsStreamingAsync(chatCompletionOptions))
        {
            if (chatUpdate.Role.HasValue)
            {
                Console.Write($"{chatUpdate.Role.Value.ToString().ToUpperInvariant()}: ");
            }

            if (string.IsNullOrEmpty(chatUpdate.ContentUpdate)) continue;

            Console.Write(chatUpdate.ContentUpdate);
            chatStringBuilder.Append(chatUpdate.ContentUpdate);
        }
        Console.WriteLine("********************************");
        Console.WriteLine("END OF SUMMARY");
        Console.WriteLine("********************************");


        chatResponses.Add(chatStringBuilder.ToString());
        return chatStringBuilder.ToString();
    }
}