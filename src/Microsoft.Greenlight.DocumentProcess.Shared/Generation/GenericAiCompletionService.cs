using System.Text;
using Azure.AI.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Scriban;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation;

#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public class GenericAiCompletionService : IAiCompletionService
{
    private string ProcessName { get; set; }

    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<GenericAiCompletionService> _logger;
    private readonly IServiceProvider _sp;
    private Kernel? _sk;
    private readonly int _numberOfPasses = 6;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IPromptInfoService _promptInfoService;
    private readonly IKernelFactory _kernelFactory;

    private ContentNode _createdBodyContentNode;

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
        _kernelFactory = parameters.KernelFactory;
        ProcessName = processName;
    }

    public async Task<List<ContentNode>> GetBodyContentNodes(
        List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber, string sectionOrTitleText, ContentNodeType contentNodeType,
        string tableOfContentsString, Guid? metadataId, ContentNode? sectionContentNode)
    {
        var combinedBodyTextStringBuilder = new StringBuilder();

        var contentNodeId = Guid.NewGuid();
        var contentNodeSystemItemId = Guid.NewGuid();
        _createdBodyContentNode = new ContentNode
        {
            Id = contentNodeId,
            Text = string.Empty,
            Type = ContentNodeType.BodyText,
            GenerationState = ContentNodeGenerationState.InProgress,
            ContentNodeSystemItemId = contentNodeSystemItemId,
            ContentNodeSystemItem = new ContentNodeSystemItem
            {
                Id = contentNodeSystemItemId,
                ContentNodeId = contentNodeId,
                SourceReferences = [],
                ComputedSectionPromptInstructions = sectionContentNode?.PromptInstructions
            }
        };

        foreach (var sourceDocument in sourceDocuments)
        {
            sourceDocument.ContentNodeSystemItemId = _createdBodyContentNode.ContentNodeSystemItem.Id;
        }

        await foreach (var bodyContentNodeString in GetStreamingBodyContentText(sourceDocuments, sectionOrTitleNumber, sectionOrTitleText,
                           contentNodeType, tableOfContentsString, metadataId, sectionContentNode))
        {
            combinedBodyTextStringBuilder.Append(bodyContentNodeString);
        }

        var combinedBodyText = combinedBodyTextStringBuilder.ToString();
        _createdBodyContentNode.Text = combinedBodyText;
        _createdBodyContentNode.GenerationState = ContentNodeGenerationState.Completed;

        return [_createdBodyContentNode];
    }

    private async IAsyncEnumerable<string> GetStreamingBodyContentText(
        List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber, string sectionOrTitleText,
        ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId, ContentNode? sectionContentNode)
    {

        var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(ProcessName);
        var pluginSourceReferenceCollector = _sp.GetRequiredService<IPluginSourceReferenceCollector>();

        _sk = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess!.ShortName);

        if (_sk == null)
        {
            throw new InvalidOperationException("Semantic Kernel instance not set for GenericAiCompletionService");
        }
    
        _sk.PrepareSemanticKernelInstanceForGeneration(documentProcess!.ShortName);

        var sectionExample = new StringBuilder();

        // Each document in sourceDocuments has a list of Citations. Each Citation has a list of Partitions. Each of these partitions has a Relevance (double) score.
        // I want to take the top 20 documents by relevance score and use them as examples in the prompt. Bubble up the highest relevance partitions to sort the documents.

        var sourceDocumentsWithHighestScoringPartitions = sourceDocuments
            .OrderByDescending(d => d.GetHighestScoringPartitionFromCitations())
            .Take(10)
            .ToList();
        
        
        foreach (var document in sourceDocumentsWithHighestScoringPartitions)
        {
            sectionExample.AppendLine($"[EXAMPLE: Document Extract]");
            sectionExample.AppendLine(document.FullTextOutput);
            sectionExample.AppendLine($"[/EXAMPLE]");

            // Add the document to the ContentNodeSystemItem.SourceReferences collection
            _createdBodyContentNode.ContentNodeSystemItem?.SourceReferences.Add(document);
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
                prompt = await BuildMainPrompt(
                    _numberOfPasses.ToString(),
                    fullSectionName,
                    customDataString,
                    tableOfContentsString,
                    exampleString,
                    sectionContentNode.PromptInstructions);

                // Remove all the Example Documents from the prompt and make a skinny prompt without it
                // The documents are in the exampleString variable
                // We include these as source references in the ContentNodeSystemItem instead.
                var skinnyPrompt = prompt.Replace(exampleString, "[RAG Documents in Source Items Collection]", StringComparison.InvariantCultureIgnoreCase);

                // Add the computed prompt to the content node system item
                _createdBodyContentNode.ContentNodeSystemItem!.ComputedUsedMainGenerationPrompt = skinnyPrompt;

                originalPrompt = prompt;
            }
            else
            {
                var summary = await SummarizeOutput(documentProcess, string.Join("\n\n", chatResponses));
                prompt = await BuildContinuationPrompt(summary, string.Join("\n\n", lastPassResponse), (i + 1).ToString(), _numberOfPasses.ToString(), originalPrompt);
            }

            var responseLine = "";
            await foreach (var stringUpdate in ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(documentProcess, systemPrompt,
                               prompt, pluginSourceReferenceCollector))
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

    private async Task<string> BuildMainPrompt(string numberOfPasses,
        string fullSectionName,
        string customDataString,
        string tableOfContentsString,
        string exampleString,
        string? promptInstructions)
    {
        var mainPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationMainPrompt", ProcessName);
        var mainPrompt = mainPromptInfo.Text;

        var documentProcessName = ProcessName;

        string sectionSpecificPromptInstructions = "";
        if (!string.IsNullOrEmpty(promptInstructions))
        {
            sectionSpecificPromptInstructions = $"""
                                                 For this section, please take into account these additional instructions:
                                                 [SECTIONPROMPTINSTRUCTIONS]                                                 
                                                 {promptInstructions}
                                                 [/SECTIONPROMPTINSTRUCTIONS]

                                                 """;
        }

        var template = Template.Parse(mainPrompt);

        var result = await template.RenderAsync(new
        {
            numberOfPasses,
            fullSectionName,
            customDataString,
            tableOfContentsString,
            exampleString,
            documentProcessName,
            sectionSpecificPromptInstructions

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

        var result = await template.RenderAsync(new
        {
            summary,
            lastPassResponseJoinedByDoubleLineFeeds,
            passNumber,
            numberOfPasses,
            originalPrompt
        }, member => member.Name);

        return result;
    }

    private async Task<string> BuildSummarizePrompt(
        string originalContent
    )
    {
        var summarizePromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSummaryPrompt", ProcessName);
        var summarizePrompt = summarizePromptInfo.Text;

        var template = Template.Parse(summarizePrompt);

        var result = await template.RenderAsync(new
        {
            originalContent
        }, member => member.Name);
        return result;
    }

    private async IAsyncEnumerable<string> ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(
        DocumentProcessInfo documentProcess, string systemPrompt,
        string userPrompt, IPluginSourceReferenceCollector pluginSourceReferenceCollector)
    {

        var executionSettings =
            await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.ContentGeneration);

        var executionIdString = Guid.NewGuid().ToString();
        var kernelArguments = new KernelArguments(executionSettings)
        {
            {"System-ExecutionId", executionIdString}
        };

        await foreach (var update in _sk.InvokePromptStreamingAsync(userPrompt, kernelArguments))
        {
            yield return update.ToString();
        }

        var executionId = Guid.Parse(executionIdString);
        var sourceReferenceItems = pluginSourceReferenceCollector.GetAll(executionId);
        if (sourceReferenceItems.Count > 0)
        {
            _createdBodyContentNode.ContentNodeSystemItem!.SourceReferences.AddRange(sourceReferenceItems);
        }

        pluginSourceReferenceCollector.Clear(executionId);
    }

    private async Task<string> SummarizeOutput(DocumentProcessInfo documentProcess, string originalContent)
    {
        // Using a streaming OpenAi ChatCompletion, summarize the originalContent with up to 8000 tokens and return the summary
        // No need to render templates for these two prompts as they don't have any placeholders
        var systemPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSystemPrompt", ProcessName);
        var systemPrompt = systemPromptInfo.Text;

        var summarizePrompt = await BuildSummarizePrompt(originalContent);

        var executionSettings =
            await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.Summarization);

        var summaryResult = "";
        var chatStringBuilder = new StringBuilder();

        Console.WriteLine("********************************");
        Console.WriteLine("SUMMARY SO FAR:");
        Console.WriteLine("********************************");

        await foreach (var update in _sk.InvokePromptStreamingAsync(summarizePrompt, new KernelArguments(executionSettings)))
        {
            if (string.IsNullOrEmpty(update.ToString())) continue;

            Console.Write(update);
            chatStringBuilder.Append(update);
        }

        Console.WriteLine("********************************");
        Console.WriteLine("END OF SUMMARY");
        Console.WriteLine("********************************");

        return chatStringBuilder.ToString();
    }
}
