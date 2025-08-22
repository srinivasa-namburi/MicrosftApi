using Azure.AI.OpenAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Helpers;
using System.Text.Json;
using Microsoft.Greenlight.Shared.Enums;
// Duplicate using removed
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI; // AzureOpenAIPromptExecutionSettings
using Microsoft.SemanticKernel.Connectors.OpenAI; // ToolCallBehavior, OpenAI behavior types
using Microsoft.SemanticKernel.ChatCompletion; // IChatCompletionService, ChatHistory
using Scriban;
using System.Text;
using Microsoft.Greenlight.Shared.Extensions;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation;

#pragma warning disable SKEXP0011
#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001
public class GenericAiCompletionService : IAiCompletionService
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
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
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        AiCompletionServiceParameters<GenericAiCompletionService> parameters,
        string processName)
    {
        _dbContextFactory = dbContextFactory;
        _serviceConfigurationOptions = parameters.ServiceConfigurationOptions.Value;
        _openAIClient = parameters.OpenAIClient;
        _sp = parameters.ServiceProvider;
        _logger = parameters.Logger;
        _documentProcessInfoService = parameters.DocumentProcessInfoService;
        _promptInfoService = parameters.PromptInfoService;
        _kernelFactory = parameters.KernelFactory;
        ProcessName = processName;
        _dbContext = dbContextFactory.CreateDbContext();
    }

    /// <summary>
    /// Generate body content nodes from a heterogeneous set of source references (Kernel Memory + Vector Store).
    /// </summary>
    public async Task<List<ContentNode>> GetBodyContentNodes(
        List<SourceReferenceItem> sourceReferences,
        string sectionOrTitleNumber, string sectionOrTitleText, ContentNodeType contentNodeType,
        string tableOfContentsString, Guid? metadataId, ContentNode? sectionContentNode)
    {
        _logger.LogInformation("Starting GetBodyContentNodes for section {SectionNumber} - {SectionTitle} with {SourceReferenceCount} source references", 
            sectionOrTitleNumber, sectionOrTitleText, sourceReferences.Count);

        try
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

            _logger.LogDebug("Created content node {ContentNodeId} for section {SectionNumber} - {SectionTitle}", 
                contentNodeId, sectionOrTitleNumber, sectionOrTitleText);

            var documentLikeSources = sourceReferences
                .Where(r => r is DocumentProcessRepositorySourceReferenceItem or DocumentLibrarySourceReferenceItem or VectorStoreAggregatedSourceReferenceItem)
                .ToList();

            _logger.LogDebug("Filtered to {DocumentLikeSourceCount} document-like sources from {TotalSourceCount} total sources", 
                documentLikeSources.Count, sourceReferences.Count);

            foreach (var kmDoc in documentLikeSources.OfType<KernelMemoryDocumentSourceReferenceItem>())
            {
                kmDoc.ContentNodeSystemItemId = _createdBodyContentNode.ContentNodeSystemItem!.Id;
            }

            _logger.LogDebug("Starting streaming body content text generation");

            await foreach (var bodyContentNodeString in GetStreamingBodyContentText(documentLikeSources, sectionOrTitleNumber, sectionOrTitleText,
                               contentNodeType, tableOfContentsString, metadataId, sectionContentNode))
            {
                combinedBodyTextStringBuilder.Append(bodyContentNodeString);
            }

            var combinedBodyText = combinedBodyTextStringBuilder.ToString();
            _createdBodyContentNode.Text = combinedBodyText;
            _createdBodyContentNode.GenerationState = ContentNodeGenerationState.Completed;

            _logger.LogInformation("Successfully generated body content with {TextLength} characters for section {SectionNumber} - {SectionTitle}", 
                combinedBodyText.Length, sectionOrTitleNumber, sectionOrTitleText);

            return [_createdBodyContentNode];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetBodyContentNodes for section {SectionNumber} - {SectionTitle}", 
                sectionOrTitleNumber, sectionOrTitleText);
            throw;
        }
    }

    private async IAsyncEnumerable<string> GetStreamingBodyContentText(
        List<SourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber, string sectionOrTitleText,
        ContentNodeType contentNodeType, string tableOfContentsString, Guid? metadataId, ContentNode? sectionContentNode)
    {
        _logger.LogDebug("Starting GetStreamingBodyContentText for section {SectionNumber} - {SectionTitle}", 
            sectionOrTitleNumber, sectionOrTitleText);

        DocumentProcessInfo documentProcess;
        IPluginSourceReferenceCollector pluginSourceReferenceCollector;
        string systemPrompt;
        string exampleString;
        string originalPrompt = "";
        var chatResponses = new List<string>();
        var lastPassResponse = new List<string>();
        string customDataString;
        string fullSectionName;

        // Initialize all variables outside the main loop to avoid try-catch with yield
        try
        {
            documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(ProcessName);
            
            if (documentProcess == null)
            {
                _logger.LogError("Document process {ProcessName} not found", ProcessName);
                throw new InvalidOperationException($"Document process {ProcessName} not found");
            }

            _logger.LogDebug("Retrieved document process info for {ProcessName}", ProcessName);

            pluginSourceReferenceCollector = _sp.GetRequiredService<IPluginSourceReferenceCollector>();

            _sk = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess!.ShortName);

            if (_sk == null)
            {
                _logger.LogError("Semantic Kernel instance is null for document process {ProcessName}", ProcessName);
                throw new InvalidOperationException($"Semantic Kernel instance not set for GenericAiCompletionService for process {ProcessName}");
            }

            _logger.LogDebug("Retrieved Semantic Kernel instance for document process {ProcessName}", ProcessName);
        
            _sk.PrepareSemanticKernelInstanceForGeneration(documentProcess!.ShortName);

            var sectionExample = new StringBuilder();

            // Each document in sourceDocuments has a list of Citations. Each Citation has a list of Partitions. Each of these partitions has a Relevance (double) score.
            // I want to take the top 20 documents by relevance score and use them as examples in the prompt. Bubble up the highest relevance partitions to sort the documents.

            var sourceDocumentsWithHighestScoringPartitions = sourceDocuments
                    .OrderByDescending(d => d.GetHighestRelevanceScore())
                    .Take(10)
                    .ToList();

            _logger.LogDebug("Selected top {SelectedDocumentCount} documents with highest relevance scores from {TotalDocumentCount} total documents", 
                sourceDocumentsWithHighestScoringPartitions.Count, sourceDocuments.Count);
        
            foreach (var document in sourceDocumentsWithHighestScoringPartitions)
            {
                sectionExample.AppendLine($"[EXAMPLE: Document Extract]");
                // Determine example text depending on source type
                string exampleText = document switch
                {
                    KernelMemoryDocumentSourceReferenceItem km => km.FullTextOutput,
                    VectorStoreAggregatedSourceReferenceItem vs => string.Join("", vs.Chunks.Select(c => c.Text)),
                    _ => string.Empty
                };
                if (!string.IsNullOrWhiteSpace(exampleText))
                {
                    sectionExample.AppendLine(exampleText);
                }
                sectionExample.AppendLine($"[/EXAMPLE]");

                // Ensure ContentNodeSystemItem is initialized before accessing it
                if (_createdBodyContentNode.ContentNodeSystemItem == null)
                {
                    _createdBodyContentNode.ContentNodeSystemItem = new ContentNodeSystemItem
                    {
                        Id = Guid.NewGuid(),
                        ContentNodeId = _createdBodyContentNode.Id,
                        SourceReferences = [],
                        ComputedSectionPromptInstructions = sectionContentNode?.PromptInstructions
                    };
                }

                _createdBodyContentNode.ContentNodeSystemItem.SourceReferences.Add(document);
            }

            exampleString = sectionExample.ToString();
            var systemPromptInfo =
                await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync("SectionGenerationSystemPrompt", ProcessName);
            systemPrompt = systemPromptInfo?.Text ?? string.Empty;

            _logger.LogDebug("Retrieved system prompt for process {ProcessName}: {HasSystemPrompt}", 
                ProcessName, !string.IsNullOrEmpty(systemPrompt));

            var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);

            customDataString = "No custom data available for this query";

            if (documentMetaData != null && !string.IsNullOrEmpty(documentMetaData.MetadataJson))
            {
                customDataString = documentMetaData.MetadataJson;
            }

            fullSectionName = string.IsNullOrEmpty(sectionOrTitleNumber) ? sectionOrTitleText : $"{sectionOrTitleNumber} {sectionOrTitleText}";

            _logger.LogDebug("Starting {NumberOfPasses} passes for section {FullSectionName}", _numberOfPasses, fullSectionName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in initialization for GetStreamingBodyContentText for section {SectionNumber} - {SectionTitle}", 
                sectionOrTitleNumber, sectionOrTitleText);
            throw;
        }

        // Main generation loop - moved outside try-catch to allow yielding
        for (var i = 0; i < _numberOfPasses; i++)
        {
            _logger.LogDebug("Starting pass {PassNumber} of {NumberOfPasses} for section {FullSectionName}", 
                i + 1, _numberOfPasses, fullSectionName);

            string prompt;
            if (i == 0)
            {
                prompt = await BuildMainPrompt(
                    _numberOfPasses.ToString(),
                    fullSectionName,
                    customDataString,
                    tableOfContentsString,
                    exampleString,
                    sectionContentNode?.PromptInstructions);

                prompt += """
                          You may call tools. After tools finish, always produce a concise final answer for the user in natural language. Never end your turn with only tool output.
                          """;

                // Remove all the Example Documents from the prompt and make a skinny prompt without it
                // The documents are in the exampleString variable
                // We include these as source references in the ContentNodeSystemItem instead.
                // Add a check before calling Replace
                if (!string.IsNullOrEmpty(exampleString))
                {
                    var skinnyPrompt = prompt.Replace(exampleString, "[RAG Documents in Source Items Collection]", StringComparison.InvariantCultureIgnoreCase);
                    // Add the computed prompt to the content node system item
                    _createdBodyContentNode.ContentNodeSystemItem!.ComputedUsedMainGenerationPrompt = skinnyPrompt;
                }
                else
                {
                    // If exampleString is empty, just use the original prompt
                    _createdBodyContentNode.ContentNodeSystemItem!.ComputedUsedMainGenerationPrompt = prompt;
                }

                originalPrompt = prompt;
            }
            else
            {
                _logger.LogInformation($"Summarizing output for section {sectionOrTitleNumber} pass {i}");
                var summary = await SummarizeOutput(documentProcess, string.Join("\n\n", chatResponses));
                prompt = await BuildContinuationPrompt(summary, string.Join("\n\n", lastPassResponse), (i + 1).ToString(), _numberOfPasses.ToString(), originalPrompt);
            }

            _logger.LogInformation($"Writing output for section {sectionOrTitleNumber} pass {i}");

            var responseLine = "";
            await foreach (var stringUpdate in ReturnCompletionsForPromptWithSemanticKernelFunctionCalling(documentProcess, systemPrompt,
                               prompt, pluginSourceReferenceCollector))
            {
                
                // Continue building the update until we reach a new line
                responseLine += stringUpdate;

                // If the response contains the [*COMPLETE*] tag, we can stop the conversation
                if (responseLine.Contains("[*COMPLETE*]", StringComparison.InvariantCultureIgnoreCase))
                {
                    _logger.LogDebug("Found completion tag in response for section {FullSectionName}, ending generation", fullSectionName);
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

        _logger.LogInformation("Completed all {NumberOfPasses} passes for section {FullSectionName}", _numberOfPasses, fullSectionName);
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

        // Collect all results first, then yield them to avoid try-catch with yield
        var results = new List<string>();
        
        try
        {
            var chatService = _sk!.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddSystemMessage(systemPrompt);
            history.AddUserMessage(userPrompt);

            await foreach (var text in SemanticKernelStreamingHelper.StreamChatWithManualToolInvocationAsync(
                chatService, history, executionSettings, _sk))
            {
                results.Add(text);
            }
        }
        finally
        {
            // nothing
        }

        // Now yield all the collected results
        foreach (var content in results)
        {
            yield return content;
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

        var chatStringBuilder = new StringBuilder();

        var chatService = _sk!.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(summarizePrompt);

        await foreach (var text in SemanticKernelStreamingHelper.StreamTextAsync(
            chatService.GetStreamingChatMessageContentsAsync(history, executionSettings, _sk)))
        {
            chatStringBuilder.Append(text);
        }

        _logger.LogInformation("Done summarizing output");

        return chatStringBuilder.ToString();
    }
}
