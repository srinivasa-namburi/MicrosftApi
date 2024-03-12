using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProjectVico.V2.Plugins.Shared;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Helpers;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Models.Enums;

namespace ProjectVico.V2.Plugins.Default.NuclearDocs.Services.AiCompletionService;

public class MultiPassLargeReceiveContextAiCompletionService : IAiCompletionService
{
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly OpenAIClient _openAIClient;
    private readonly TableHelper _tableHelper;
    private readonly DocGenerationDbContext _dbContext;
    private Kernel _sk;
    private readonly IServiceProvider _sp;
    private int _numberOfPasses = 6;

    public MultiPassLargeReceiveContextAiCompletionService(
        IOptions<ServiceConfigurationOptions> serviceConfigurationOptions,
        [FromKeyedServices("openai-planner")] OpenAIClient openAIClient,
        TableHelper tableHelper,
        DocGenerationDbContext dbContext,
        IServiceProvider sp)
    {
        _serviceConfigurationOptions = serviceConfigurationOptions.Value;
        _openAIClient = openAIClient;
        _tableHelper = tableHelper;
        _dbContext = dbContext;
        _sp = sp;
    }

    public async Task<List<ContentNode>> GetBodyContentNodes(List<ReportDocument> documents,
        string sectionOrTitleNumber, string sectionOrTitleText, ContentNodeType contentNodeType,
        string tableOfContentsString, Guid? metadataId)
    {
        using var scope = _sp.CreateScope();
        var plugins = new KernelPluginCollection();

        plugins.AddRegisteredPluginsToKernelPluginCollection(_sp, excludePluginType: typeof(NuclearDocumentRepositoryPlugin));
        _sk = new Kernel(_sp, plugins);

        var sectionExample = new StringBuilder();
        var firstDocuments = documents.Take(3).ToList();

        foreach (var document in firstDocuments)
        {
            document.Content = _tableHelper.ReplaceTableReferencesWithHtml(document.Content);
        }

        foreach (var document in firstDocuments)
        {
            sectionExample.AppendLine($"[EXAMPLE: {document.Title}]");
            sectionExample.AppendLine(document.Content);
            sectionExample.AppendLine();
        }

        var exampleString = sectionExample.ToString();
        string originalPrompt = "";
        var chatResponses = new List<string>();
        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors ('SMR') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Provide responses that can be copied directly into an environmental report.";

        var lastPassResponse = new List<string>();
        
        var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);

        string customDataString = "No custom data available for this query";

        if (documentMetaData != null && !string.IsNullOrEmpty(documentMetaData.MetadataJson))
        {
            customDataString = documentMetaData.MetadataJson;
        }
        
        for (int i = 0; i < _numberOfPasses; i++)
        {
      

            string prompt;
            if (i == 0)
            {
                prompt = $"""
                         
                         This is the initial query in a multi-pass conversation. You are not expected to return the full output in this pass. 
                         However, please be as complete as possible in your response for this pass. For this task, including this initial query,
                         we will be performing {_numberOfPasses} passes to form a complete response. This is the first pass.
                         
                         There will be additional queries asking to to expand on a combined summary of the output you provide here and 
                         further summaries from later responses.
                         
                         Below, there are several sections from previous applications denoted by [EXAMPLE: <section title>].

                         Using this information, write a similar section(sub-section) or chapter(section),
                         depending on which is most appropriate to the query.
                         
                         For customizing the output so that it pertains to this project, please use tool calling/functions as supplied to you
                         in the list of available functions. If you need additional data, please request it using the [DETAIL: <dataType>] tag.
                         
                         Custom data for this project follows in JSON format between the [CUSTOMDATA] and [/CUSTOMDATA] tags. IGNORE the following fields:
                         DocumentProcessName, MetadataModelName, DocumentGenerationRequestFullTypeName, ID, AuthorOid.
                         
                         [CUSTOMDATA]
                         {customDataString}
                         [/CUSTOMDATA]
                         
                         You are writing the section {sectionOrTitleNumber} - {sectionOrTitleText}. The section examples may contain input from 
                         additional sub-sections in addition to the specific section you are writing.
                         
                         In between the [TOC] and [/TOC] tags below, you will find a table of contents for the entire document.
                         Please make sure to use this table of contents to ensure that the section you are writing fits in with the rest of the document,
                         and to avoid duplicating content that is already present in the document. Pay particular attention to neighboring sections and the
                         parent title of the section you're writing. If you see references to sections in the section you're writing,
                         please use this TOC to validate chapter and section numbers and to ensure that the references are correct. Please don't 
                         refer to tables or sections that are not in the TOC provided here.
                         
                         [TOC]
                         {tableOfContentsString.TrimEnd()}
                         [/TOC]
                         
                         Be as verbose as necessary to include all required information. Try to be very complete in your response, considering all source data. We
                         are looking for full sections or chapters - not short summaries.

                         For headings, remove the numbering and any heading prefixes like "Section" or "Chapter" from the heading text.

                         Make sure to clean up the text inside the Text property of the generated json array which the plugin returns.
                         In particular, make sure it complies with UTF-8 encoding. Paragraphs should be separateed by two newlines (\n\n)
                         For lists, use only * and - characters as bullet points, and make sure to have a space after the bullet point character.

                         Format the text with Markdown syntax. For example, use #, ##, ### for headings, * and - for bullet points, etc.
                         
                         For table outputs, look at the example content and look for tables that match the table description. Please render them inline or at the end
                         of the text as Markdown Tables. If they are too complex, you can use HTML tables instead that accomodate things like rowspan and colspan. 
                         You should adopt the contents of these tables to fit any custom inputs you have received regarding location, size, reactor sepcifics
                         and so on. If you have no such inputs, consider the tables as they are in the examples as the default.
                         
                         If you are missing details to write specific portions of the text, please indicate that with [DETAIL: <dataType>] -
                         and put the type of data needed in the dataType parameter. Make sure to look at all the source content before you decide you lack details!
                         
                         Be concise in these DETAIL requests - no long sentences, only data types with a one or two word description of what's missing.
                         
                         If you believe the output is complete (and this is the last needed pass to complete the whole section), please end your response with the following text on a 
                         new line by itself:
                         [*COMPLETE*]
                         
                         {exampleString}
                         
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
                            take care not to repeat the same information you've already provided (as detailed in the summary). Also ignore the first two paragraphs of the prompt detailing
                            how to respond to the first pass query. This is pass number {i + 1} of {_numberOfPasses} - take that into account when you respond.
                            
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

            lastPassResponse = await ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(systemPrompt, prompt);
            chatResponses.AddRange(lastPassResponse);


            if (chatResponses.Last().Contains("[*TO BE CONTINUED*]", StringComparison.InvariantCultureIgnoreCase))
            {
                chatResponses[^1] = chatResponses.Last().Replace("[*TO BE CONTINUED*]", "", StringComparison.InvariantCultureIgnoreCase);
            }

            // If the response contains the [*COMPLETE*] tag, we can stop the conversation
            if (chatResponses.Last().Contains("[*COMPLETE*]", StringComparison.InvariantCultureIgnoreCase))
            {
                // Remove the [*COMPLETE*] tag from the response
                chatResponses[^1] = chatResponses.Last().Replace("[*COMPLETE*]", "");
                break;
            }
        }

        var combinedBodyTextStringBuilder = new StringBuilder();

        foreach (var chatResponse in chatResponses)
        {
            combinedBodyTextStringBuilder.Append(chatResponse);
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

    private async Task<List<string>> ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(string systemPrompt,
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

        var chatResponses = new List<string>();
        var chatStringBuilder = new StringBuilder();

        await foreach (var update in _sk.InvokePromptStreamingAsync(userPrompt, new KernelArguments(openAiExecutionSettings)))
        {
            Console.Write(update);
            chatStringBuilder.Append(update);
        }

        chatResponses.Add(chatStringBuilder.ToString());
        return chatResponses;


    }

    private async Task<string> SummarizeOutput(string originalContent)
    {
        // using a streaming OpenAi ChatCompletion, summarize the originalContent with up to 8000 tokens and return the summary
        var systemPrompt =
            "[SYSTEM]: This is a chat between an intelligent AI bot specializing in assisting with producing environmental reports for Small Modular nuclear Reactors (''SMR'') and one or more participants. The AI has been trained on GPT-4 LLM data through to April 2023 and has access to additional data on more recent SMR environmental report samples. Try to be complete with your responses. Provide responses that can be copied directly into an environmental report, so no polite endings like 'i hope that helps', no beginning with 'Sure, I can do that', etc.\"";

        var summarizePrompt = "When responding, do not include ASSISTANT: or initial greetings/information about the reply. Only the content/summary, please. Please summarize the following text so it can form the basis of further expansion: \n\n" + originalContent + "\n\n";

        var chatResponses = new List<string>();

        var chatCompletionOptions = new ChatCompletionsOptions
        {
            Messages =
            {
                new ChatRequestSystemMessage(systemPrompt),
                new ChatRequestUserMessage(summarizePrompt)
            },
            DeploymentName = _serviceConfigurationOptions.OpenAi.DocGenModelDeploymentName,
            MaxTokens = 8000,
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