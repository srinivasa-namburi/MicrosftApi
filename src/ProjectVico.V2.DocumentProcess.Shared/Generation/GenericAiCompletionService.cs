using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ProjectVico.V2.DocumentProcess.Shared.Plugins.KmDocs;
using ProjectVico.V2.Shared.Configuration;
using ProjectVico.V2.Shared.Contracts;
using ProjectVico.V2.Shared.Data.Sql;
using ProjectVico.V2.Shared.Enums;
using ProjectVico.V2.Shared.Extensions;
using ProjectVico.V2.Shared.Models;
using ProjectVico.V2.Shared.Services;
using Scriban;

namespace ProjectVico.V2.DocumentProcess.Shared.Generation;

public class GenericAiCompletionService : IAiCompletionService
{
    private string ProcessName { get; set; }

    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly OpenAIClient _openAIClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<GenericAiCompletionService> _logger;
    private readonly IServiceProvider _sp;
    private Kernel? _sk;
    private readonly int _numberOfPasses = 6;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IPromptInfoService _promptInfoService;

    public GenericAiCompletionService(
        AiCompletionServiceParameters<GenericAiCompletionService> parameters,
        string processName)
    {
        _serviceConfigurationOptions = parameters.ServiceConfigurationOptions.Value;
        _openAIClient = parameters.OpenAIClient;
        _dbContext = parameters.DbContext;
        _sp = parameters.ServiceProvider;
        _logger = parameters.Logger;
        _documentProcessInfoService = parameters.DocumentProcessInfoService;
        _promptInfoService = parameters.PromptInfoService;
        ProcessName = processName;
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
        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(ProcessName);

        plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(_sp, documentProcess, excludedPluginTypes: [typeof(KmDocsPlugin)]);

        _sk = new Kernel(_sp, plugins);

        var sectionExample = new StringBuilder();
        var firstDocuments = documents.Take(20).ToList();

        foreach (var document in firstDocuments)
        {
            sectionExample.AppendLine($"[EXAMPLE: Document Extract]");
            sectionExample.AppendLine(document.Content);
            sectionExample.AppendLine();
        }

        var exampleString = sectionExample.ToString();
        var originalPrompt = "";
        var chatResponses = new List<string>();
        var systemPromptInfo =
            await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSystemPrompt", ProcessName);
        var systemPrompt = systemPromptInfo.Text;



        var lastPassResponse = new List<string>();
        var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);

        var customDataString = "No custom data available for this query";

        if (documentMetaData != null && !string.IsNullOrEmpty(documentMetaData.MetadataJson))
        {
            customDataString = documentMetaData.MetadataJson;
        }

        var fullSectionName = "";

        fullSectionName = string.IsNullOrEmpty(sectionOrTitleNumber) ? sectionOrTitleText : $"{sectionOrTitleNumber} {sectionOrTitleText}";

        for (var i = 0; i < _numberOfPasses; i++)
        {
            string prompt;
            if (i == 0)
            {

                prompt = await BuildMainPrompt(_numberOfPasses.ToString(), fullSectionName, customDataString, tableOfContentsString, exampleString);
                originalPrompt = prompt;
            }
            else
            {
                var summary = await SummarizeOutput(string.Join("\n\n", chatResponses));
                prompt = await BuildContinuationPrompt(summary, string.Join("\n\n", lastPassResponse), (i + 1).ToString(), _numberOfPasses.ToString(), originalPrompt);
            }

            var responseLine = "";
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

    private async Task<string> BuildMainPrompt(
        string numberOfPasses,
        string fullSectionName,
        string customDataString,
        string tableOfContentsString,
        string exampleString)
    {
        var mainPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationMainPrompt", ProcessName);
        var mainPrompt = mainPromptInfo.Text;

        var template = Template.Parse(mainPrompt);

        var result = template.Render(new
        {
            numberOfPasses,
            fullSectionName,
            customDataString,
            tableOfContentsString,
            exampleString

        }, member => member.Name);

        return result;
    }

    private async Task<string> BuildContinuationPrompt(
        string summary,
        string lastPassResponseJoinedByDoubleLineFeeds,
        string passNumber,
        string numberOfPasses,
        string originalPrompt)
    {
        var continuationPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationMultiPassContinuationPrompt", ProcessName);
        var continuationPrompt = continuationPromptInfo.Text;

        var template = Template.Parse(continuationPrompt);

        var result = template.Render(new
        {
            summary,
            lastPassResponseJoinedByDoubleLineFeeds,
            passNumber,
            numberOfPasses,
            originalPrompt
        }, member => member.Name);

        return result;
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
        // Using a streaming OpenAi ChatCompletion, summarize the originalContent with up to 8000 tokens and return the summary
        // No need to render templates for these two prompts as they don't have any placeholders
        var systemPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSystemPrompt", ProcessName);
        var systemPrompt = systemPromptInfo.Text;

        var summarizePromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSummaryPrompt", ProcessName);
        var summarizePrompt = summarizePromptInfo.Text;

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

        var chatStringBuilder = new StringBuilder();
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

        return chatStringBuilder.ToString();
    }
}