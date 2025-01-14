using System.Text;
using Azure.AI.OpenAI;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Services;
using Scriban;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Extensions;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation.Agentic;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

public class AgentAiCompletionService : IAiCompletionService
{
    private readonly string ProcessName;
    private readonly ServiceConfigurationOptions _serviceConfigurationOptions;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly DocGenerationDbContext _dbContext;
    private readonly ILogger<AgentAiCompletionService> _logger;
    private readonly IServiceProvider _sp;
    private readonly Kernel _sk;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IPluginSourceReferenceCollector _pluginSourceReferenceCollector;
    private readonly IPromptInfoService _promptInfoService;
    private const int InitialBlockSize = 100;
    private string _executionIdString;

    private ContentNode _createdBodyContentNode;
    private AgentGroupChat _agentGroupChat;

    private ContentStatePlugin _contentStatePlugin;
    
    public AgentAiCompletionService(
        AiCompletionServiceParameters<AgentAiCompletionService> parameters,
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

        var documentProcess = _documentProcessInfoService
            .GetDocumentProcessInfoByShortNameAsync(ProcessName).Result;
        _sk = _sp.GetRequiredServiceForDocumentProcess<Kernel>(documentProcess!.ShortName);

        _sk.Plugins.Clear();
        _sk.Plugins.AddSharedAndDocumentProcessPluginsToPluginCollection(_sp, documentProcess);

        _executionIdString = Guid.NewGuid().ToString();
        _pluginSourceReferenceCollector = _sp.GetRequiredService<IPluginSourceReferenceCollector>();
    }

    public async Task<List<ContentNode>> GetBodyContentNodes(
        List<DocumentProcessRepositorySourceReferenceItem> sourceDocuments,
        string sectionOrTitleNumber,
        string sectionOrTitleText,
        ContentNodeType contentNodeType,
        string tableOfContentsString,
        Guid? metadataId,
        ContentNode? sectionContentNode)
    {
        // Prepare an empty ContentNode for the final text
        InitializeContentNode(sectionContentNode);

        // Extract custom metadata (if any)
        var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);
        var customDataString = documentMetaData?.MetadataJson ?? "No custom data available for this query";

        // Create a user-facing name for the section
        var fullSectionName = string.IsNullOrEmpty(sectionOrTitleNumber)
            ? sectionOrTitleText
            : $"{sectionOrTitleNumber} {sectionOrTitleText}";

        var sectionExample = new StringBuilder();

        // Compress source documents down to a lower number based on ranking
        var sourceDocumentsWithHighestScoringPartitions = sourceDocuments
            .OrderByDescending(d => d.GetHighestScoringPartitionFromCitations())
            .Take(5)
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
        var numberOfPasses = 1;

        // Build our "main prompt"
        var mainPrompt = await BuildMainPrompt(
            numberOfPasses.ToString(),
            fullSectionName,
            customDataString,
            tableOfContentsString,
            exampleString,
            sectionContentNode?.PromptInstructions
        );

        _contentStatePlugin = new ContentStatePlugin(exampleString, InitialBlockSize);

        // Create the AgentGroupChat with your new strategies
        _agentGroupChat = SetupAgents(fullSectionName, mainPrompt, exampleString);

        var initialMessage = "Please follow your instructions to start the conversation.";

        // Add that user message to the conversation
        _agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, initialMessage));

        // Now, rely on the conversation framework (SelectionStrategy + TerminationStrategy)
        // to orchestrate who speaks next. We only do a single loop over the conversation stream.
        var cancellationToken = new CancellationToken(false);
        await foreach (var message in _agentGroupChat.InvokeAsync(cancellationToken))
        {
            await ProcessAgentMessage(message);
        }

        var executionId = Guid.Parse(_executionIdString);
        var sourceReferenceItems = _pluginSourceReferenceCollector.GetAll(executionId);
        if (sourceReferenceItems.Count > 0)
        {
            _createdBodyContentNode.ContentNodeSystemItem!.SourceReferences.AddRange(sourceReferenceItems);
        }

        _pluginSourceReferenceCollector.Clear(executionId);

        // Update the final content node
        var finalContent = _contentStatePlugin.GetAssembledContent();
        if (!string.IsNullOrEmpty(finalContent))
        {
            // Remove all instances of [*COMPLETE*] from the final content
            finalContent = finalContent.Replace("[*COMPLETE*]", string.Empty);

            // Update the ContentNode with the final content and mark it as completed
            _createdBodyContentNode.Text = finalContent;
            _createdBodyContentNode.GenerationState = ContentNodeGenerationState.Completed;
        }

        return new List<ContentNode> { _createdBodyContentNode };
    }

    private async Task ProcessAgentMessage(ChatMessageContent message)
    {
        var content = message.Content;

        switch (message.AuthorName)
        {
            case "WriterAgent" when content.Contains("[ContentOutput"):
                foreach (var (outputContent, attributes) in ExtractTagsWithAttributes(content, "ContentOutput"))
                {
                    if (attributes.TryGetValue("sequence", out var seqStr) &&
                        int.TryParse(seqStr, out var sequenceNumber))
                    {
                        if (!string.IsNullOrEmpty(outputContent))
                        {
                            _contentStatePlugin.StoreSequenceContent(sequenceNumber, outputContent);
                            _logger.LogInformation($"WriterAgent produced output - Stored content for sequence {sequenceNumber}");
                        }
                    }
                    else
                    {
                        _logger.LogWarning("ContentOutput tag found without valid sequence number");
                    }
                }
                break;

            case "ReviewerAgent" when content.Contains("[COMPLETE]"):
                _logger.LogInformation("ReviewerAgent signaled completion - ending section output");
                break;
        }
    }

    /// <summary>
    /// Sets up the AgentGroupChat with your custom strategies and all three agents.
    /// </summary>
    private AgentGroupChat SetupAgents(string fullSectionName, string mainPrompt, string sourceDocuments)
    {
        _sk.ImportPluginFromObject(_contentStatePlugin, "ContentState");

        var limitedKernel = _sk.Clone();
        limitedKernel.Plugins.Clear();

        limitedKernel.ImportPluginFromObject(_contentStatePlugin, "ContentState");

        var agentGroupChat = new AgentGroupChat
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new ContentGenerationAgentSelectionStrategy(_logger),
                TerminationStrategy = new CompleteTagTerminationStrategy()
            }
        };

        var contentStateKernelPlugin = _sk.Plugins.FirstOrDefault(x => x.Name == "ContentState");
        var contentStateKernelFunctionNames = contentStateKernelPlugin?.GetFunctionsMetadata().Select(x => x.Name).ToList();

        var contentStateKernelFunctions = new List<KernelFunction>();
        foreach (var functionName in contentStateKernelFunctionNames!)
        {
            contentStateKernelPlugin!.TryGetFunction(functionName, out KernelFunction function);
            if (function != null)
            {
                contentStateKernelFunctions.Add(function);
            }
        }

        var regularOpenAiSettings = new AzureOpenAIPromptExecutionSettings()
        {
            MaxTokens = 4000,
            //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(contentStateKernelFunctions),
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var regularKernelArguments = new KernelArguments(regularOpenAiSettings)
        {
            {"System-ExecutionId", _executionIdString}
        };

        var knowledgeRetrievalOpenAiSettings = new AzureOpenAIPromptExecutionSettings()
        {
            MaxTokens = 4000,
            //FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
        };

        var knowledgeRetrievalKernelArguments = new KernelArguments(knowledgeRetrievalOpenAiSettings)
        {
            {"System-ExecutionId", _executionIdString}
        };

        var loggerFactory = _sp.GetRequiredService<ILoggerFactory>();

        var kmdocsPluginName = $"{ProcessName.Replace(".", "_").Replace("-", "_").Replace(" ", "_")}__KmDocsPlugin";

        var knowledgeRetrievalAgent = new ChatCompletionAgent
        {
            Name = "KnowledgeRetrievalAgent",
            Instructions = $"""
            You are a knowledge assistant with access to:
            - Source documents through ContentState.GetSourceDocuments()
            - The {kmdocsPluginName} plugin with multiple additional research on previous examples of what
               you are producing. Use this to ask questions about a topic (via Tool Calling)
            - The native_DocumentLibrary plugin to access additional document libraries with various information (via Tool Calling) beyond 
               the source documents
            - General knowledge about the topic you are working on
            - Current content through ContentState.GetAssembledContent()

            When providing knowledge:
            - Specify which sequence number this knowledge is for
            - Structure your output as:
               [KNOWLEDGE sequence=X]
               Relevant information and analysis...
               Please summarize the knowledge instead of providing it verbatim.
               [/KNOWLEDGE]

            When requested for specific information:
            - Use GetSequenceWithContext() to understand the context for the knowledge you're being asked to provide
            - Focus on the specific sequence requested
            - Provide targeted information that addresses gaps or revision needs
            - Limit yourself to the section currently being worked on - {fullSectionName}

            When addressing other agents directly, start your message with "ReviewerAgent:" or "WriterAgent:"
            Only do this when not starting with a tag.
            """,
            Kernel = _sk,
            Arguments = knowledgeRetrievalKernelArguments,
            LoggerFactory = loggerFactory
        };

        var writerAgent = new ChatCompletionAgent
        {
            Name = "WriterAgent",
            Instructions = $"""
            You are an expert writer responsible for creating high-quality document sections.
            You have access to the following functions through the "ContentState" plugin:
            - GetAssembledContent(): Get all content written so far
            - GetSequenceContent(number): Get content for a specific sequence
            - GetSequenceWithContext(number): Get content with surrounding context
            - GetNextSequenceNumber(): Get the next available sequence number when producing new content
            - StoreSequenceContent(number, content): Store content for a sequence

            [MAIN_PROMPT]
            {mainPrompt}
            [/MAIN_PROMPT]

            When writing content:
            -  Ask KnowledgeRetrievalAgent to provide backing info for the content you're asked to produce using 
               [REQUEST_KNOWLEDGE sequence=xxx] where xxx is the sequence number you're working on. Provide the knowledge
               request inside the [REQUEST_KNOWLEDGE] tags.
            -  Structure your suggested output inside tags like this:
               [ContentOutput sequence=xxx]
               Your content here...
               [/ContentOutput]
               ALWAYS put your content inside such tags. If you have additional comments beyond your created content,
               put that outside the tags, after the closing tag.
            -  Always specify the sequence number you're writing for
            -  When writing sequences, use ContentState.GetNextSequenceNumber() to get the next available sequence number
            -  Review previous content using ContentState.GetAssembledContent() for context
            -  If you need additional knowledge to complete a sequence, ask KnowledgeRetrievalAgent with [REQUEST_KNOWLEDGE sequence=X].
               Prefer this over making assumptions or using just general knowledge.
           
            -  Look at your MAIN_PROMPT to understand the context and requirements. This is critical. It also
               includes sampled content from previous reports (inside [EXAMPLE] tags) of the same type you're producing that should
               be relevant to the section you're writing.
            -  Specifically, do NOT write content for sections outside the one you're currently working on.
            -  If asked to work on sections other than the current one - {fullSectionName}, refuse based on the bullet above.

            When you receive knowledge information ([KNOWLEDGE sequence=xxx] or [KNOWLEDGE] tags): 
            -  If a specific sequence is provided, use the sequence number to use the knowledge in the right spot. 
               Use your available plugins to retrieve the sequence.
            -  If no sequence is provided, assume it's for a future sequence. This also applies if you can't find the sequence. 
            -  If you've already written the section, rewrite it and output/store again.

            When handling revisions ([REVISE] tags):
            -  Use GetSequenceWithContext() to understand surrounding content
            -  Analyze the revision instructions carefully
            -  Maintain consistency with surrounding content
            -  Output revised content in [ContentOutput sequence=X] tags, making sure to match the sequence number
               you were asked to revise.
            -  The tag may include a heading called KNOWLEDGE: - this is additional knowledge for you to take into consideration
               when revising the section.

            When handling continue requests (with our without [CONTINUE] tag):
            -  Use GetSequenceWithContext() to understand surrounding content (in particular previous content) if needed
            -  Output the continued sequence in [ContentOutput sequence=X] tags. Use the sequence number from the CONTINUE tag. 
               If not present in CONTINUE tag or no continue tag present, use the next sequence number retrieved from ContentState.GetNextSequenceNumber().
            -  The Reviewer Agent might provide instructions inside the CONTINUE tag, specifically a summary of what has been
               written up to this point or a summary of the last content block. Use this to determine where to continue in the output. 
               Do not use this as content, but as a guide on what to write. Do not repeat the content in the CONTINUE tag. 
            -  Never just repeat what is inside the CONTINUE tags - develop the content on your own in partnership with the
               KnowledgeRetrievalAgent as outlined above. Please request knowledge when you need it. 
            -  Specifically, do NOT write content for sections outside the one you're currently working on.
            -  If asked to work on sections other than the current one - {fullSectionName}, refuse based on point 7. 
               This also applies to subsections and neighboring sections. Refuse to work on other sections than {fullSectionName}. Respond with your reasoning for doing so.

            When handling removal requests ([REMOVE] tag):
            -  Remove the sequence specified in the tag using the ContentState.RemoveSequenceContent() function
            -  Respond to ReviewerAgent directly letting it know the sequence has been removed

            When addressing other agents, start your message with "ReviewerAgent:" or "KnowledgeRetrievalAgent:"
            Only do this when not starting with a tag.
            """,
            Kernel = limitedKernel,
            Arguments = regularKernelArguments,
            LoggerFactory = loggerFactory
        };

        var reviewerAgent = new ChatCompletionAgent
        {
            Name = "ReviewerAgent",
            Instructions = $"""
            You are a reviewer with access to:
            - The original source documents through ContentState.GetSourceDocuments(). Please don't only use this - see below on how to request further knowledge.
            - All written content through ContentState.GetAssembledContent() - use sparingly, as this uses a lot of data
            - Individual sequences through ContentState.GetSequenceContent(number) - use this to get information about content already produced for a sequence
            - Context through GetSequenceWithContext(number) - this gets neighboring sequences in addition to the section you're asking about.

            Your responsibilities:
            - Review content against source documents for accuracy
            - Ensure proper coverage of topics
            - Maintain content flow and structure
            - Validate content produced against knowledge by asking through REQUEST_KNOWLEDGE as detailed below.

            When reviewing:
            - Check the current sequence against source documents (don't check for sequences that are not yet written)
            - Review how it fits with existing content
            - Respond with one of the tags from the list of tags below and obeying the following rules:
               * Only one tag per message. 
               * The instructions on how you should use the tags are directly below each closing tag in the list
               * Include your instructions to the responder between the opening and closing tag.
               * Always close the tag after your instructions. 
               * Never put additional tags inside the outer tags. 
               * Don't provide instructions on what tags to respond with - this is the responsibility of the agent you're responding to.

              LIST OF POSSIBLE RESPONSE TAGS:

              - [REQUEST_KNOWLEDGE sequence=X]
                [/REQUEST_KNOWLEDGE]
                Use this to ask for additional information to verify content accuracy or to fill in gaps.
                Don't trust your own judgment - always ask for knowledge to verify content accuracy or fill in gaps. In general you 
                should use this to validate the content of a [ContentOutput sequence=xxx] message.

              - [REVISE sequence=X]
                [/REVISE]
                * Ask for a specific sequence to be rewritten or revised. Please ground your revisions in knowledge retrieved through [REQUEST_KNOWLEDGE sequence=xxx]
                  before justifying revisions. 
                * When incorporating knowledge from KNOWLEDGE tags, please include it here (without the tags)
                  in a summarized form. Preface that knowledge with KNOWLEDGE:

              - [CONTINUE sequence=X]
                [/CONTINUE]
                * In this block, provide a SUMMARY of what has been written in the previous sections and instruct
                  the Writer Agent to continue where it left off. Use the ContentState plugin to get the previous content blocks and summarize them.
                * Do NOT ask the WriterAgent to go beyond the current section. The current section is {fullSectionName}. This includes neighboring sections and subsections.
                * If you are responding to [ContentOutput sequence=100], respond with [CONTINUE sequence=200], and so on. Use ContentState.GetSequenceContent(number) to get the next available sequence number.
                * When incorporating knowledge from KNOWLEDGE tags received, please include it here (without the tags)
                  in a summarized form. Preface that knowledge with KNOWLEDGE:
                
              - [REMOVE sequence=X]
                [/REMOVE]
                Ask for a specific sequence to be removed. Only ask for removal if revision isn't possible. Only ever ask to remove 
                the last sequence you've reviewed. 

              - [COMPLETE]
                [/COMPLETE]
                Let the user know that the content is complete. No closing tag required here. Respond only with this tag and no further content. This ends processing.
                
               END LIST OF TAGS.

            - When requesting actions for future sequences, specify the next sequence number which should always be in increments of 100
            - Specifically, do NOT ask to continue beyond the current section you're working on, which is {fullSectionName}. 
              This includes neighboring sections and subsections.
            - If you receive content for any other section beyond {fullSectionName} from the WriterAgent - ask to REMOVE it with the instructions for the [REMOVE] tag above.
            - Ignore any [*COMPLETE*] markings sent from the WriterAgent. Deciding when the section is complete is up to you. Don't worry about removing these markings.

            When you receive a KNOWLEDGE tag
            - If the content already exists (validate with ContentState.GetSequenceContent(sequenceNumber)), 
              use this to ask for a revision if neccessary with a REVISE tag as detailed above.
            - If the content for the sequence does not exist,
              ask the WriterAgent to produce the content using a CONTINUE tag as detailed above.
              
            
            Before marking as [COMPLETE]:
            - Ensure all content is consolidated
            - Verify source coverage is comprehensive (check with [REQUEST_KNOWLEDGE sequence=xxx] for each sequence - in separate messages)
            - Confirm flow and transitions are smooth
            - Ensure content is limited to the current section only - {fullSectionName}
            - Re-validate all sequences (use the ContentState plugin/tool) to ensure all content is present and accurate. Use REQUEST_KNOWLEDGE to fill in gaps and know what to ask for.

            When addressing other agents directly, start your message with "WriterAgent:" or "KnowledgeRetrievalAgent:". 
            Only do this when not starting with a tag.
            
            ALWAYS start your message with one of the tags above. If you don't, the message will be ignored.
            All your instructions and requests should be inside the tag.
            
            For information, this is the instructions the WriterAgent are using to write the content:
            [MAIN_PROMPT]
            {mainPrompt}
            [/MAIN_PROMPT]
            """,
            Kernel = limitedKernel,
            Arguments = regularKernelArguments,
            LoggerFactory = loggerFactory
        };

        agentGroupChat.AddAgent(writerAgent);
        agentGroupChat.AddAgent(knowledgeRetrievalAgent);
        agentGroupChat.AddAgent(reviewerAgent);

        return agentGroupChat;
    }

    /// <summary>
    /// Calls on the WriterAgent to unify multiple partial contents into a single consolidated text.
    /// </summary>
    private async Task ConsolidateContent(AgentGroupChat chat)
    {
        var assembledContent = _contentStatePlugin.GetAssembledContent();
        if (string.IsNullOrEmpty(assembledContent)) return;

        var consolidationPrompt = new ChatMessageContent(
            AuthorRole.User,
            $"""
             WriterAgent: Please review and consolidate all content while maintaining flow and coherence:

             {assembledContent}

             Provide a consolidated version that:
             1. Maintains all key information
             2. Improves transitions
             3. Ensures consistent style
             4. Preserves logical structure

             Enclose the consolidated content in [ContentOutput sequence=100] tags.
             """
        );

        _agentGroupChat.AddChatMessage(consolidationPrompt);
    }

    /// <summary>
    /// Prepares an empty ContentNode for the final text. 
    /// </summary>
    private void InitializeContentNode(ContentNode? sectionContentNode)
    {
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
    }

    /// <summary>
    /// Builds a scriban-based main prompt for the content generation. 
    /// </summary>
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

    /// <summary>
    /// Utility to extract text between [TAG]...[/TAG].
    /// </summary>
    private static string ExtractTagContent(string content, string tagName)
    {
        var startTag = $"[{tagName}]";
        var endTag = $"[/{tagName}]";

        var startIndex = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0) return string.Empty;

        startIndex += startTag.Length;
        var endIndex = content.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
        if (endIndex <= startIndex) return string.Empty;

        return content.Substring(startIndex, endIndex - startIndex).Trim();
    }

    // <summary>
    /// Utility to extract all instances of content between [TAG]...[/TAG] including attributes.
    /// </summary>
    private IEnumerable<(string Content, Dictionary<string, string> Attributes)> ExtractTagsWithAttributes(
        string content,
        string tagName)
    {
        var pattern = $@"\[{tagName}(?: ([^\]]*))?\](.*?)\[/{tagName}\]";
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content,
            pattern,
            System.Text.RegularExpressions.RegexOptions.Singleline);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var attributes = new Dictionary<string, string>();
            var attributesString = match.Groups[1].Value.Trim();

            if (!string.IsNullOrEmpty(attributesString))
            {
                var attributeMatches = System.Text.RegularExpressions.Regex.Matches(
                    attributesString,
                    @"(\w+)=(\w+)"
                );

                foreach (System.Text.RegularExpressions.Match attrMatch in attributeMatches)
                {
                    attributes[attrMatch.Groups[1].Value] = attrMatch.Groups[2].Value;
                }
            }

            yield return (match.Groups[2].Value.Trim(), attributes);
        }
    }

    /// <summary>
    /// Utility to parse the sequence number from "[REVISE 100]" or similar.
    /// </summary>
    private static int ExtractSequenceNumber(string content)
    {
        // Look for sequence=X in tags first
        var sequenceMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"sequence=(\d+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (sequenceMatch.Success)
            return int.Parse(sequenceMatch.Groups[1].Value);

        // Fall back to looking for [REVISE X] format
        var reviseMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"\[(?:REVISE|REQUEST_KNOWLEDGE)\s+(\d+)\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return reviseMatch.Success ? int.Parse(reviseMatch.Groups[1].Value) : InitialBlockSize;
    }
}
