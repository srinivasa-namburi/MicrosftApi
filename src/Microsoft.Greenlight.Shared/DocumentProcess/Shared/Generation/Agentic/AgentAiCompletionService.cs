using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Plugins;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Scriban;
using System.Text;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic
{
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

    public class AgentAiCompletionService : IAiCompletionService
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly string ProcessName;
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<AgentAiCompletionService> _logger;
        private readonly IServiceProvider _sp;
        private Kernel _sk;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IPluginSourceReferenceCollector _pluginSourceReferenceCollector;
        private readonly IPromptInfoService _promptInfoService;
        private readonly IMapper _mapper;
        private const int InitialBlockSize = 100;

        private readonly string _executionIdString;
        private ContentNode _createdBodyContentNode;
        private AgentGroupChat _agentGroupChat;
        private ContentStatePlugin _contentStatePlugin;
        private DocumentHistoryPlugin _documentHistoryPlugin;
        private readonly IKernelFactory _kernelFactory;

        public AgentAiCompletionService(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            AiCompletionServiceParameters<AgentAiCompletionService> parameters,
            string processName)
        {
            _sp = parameters.ServiceProvider;
            _logger = parameters.Logger;
            _documentProcessInfoService = parameters.DocumentProcessInfoService;
            _promptInfoService = parameters.PromptInfoService;
            _kernelFactory = parameters.KernelFactory;
            _dbContextFactory = dbContextFactory;
            _dbContext = _dbContextFactory.CreateDbContext();
            ProcessName = processName;
            _mapper = _sp.GetRequiredService<IMapper>();
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

            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(ProcessName);
            if (documentProcess == null)
            {
                throw new InvalidOperationException($"Document process '{ProcessName}' not found.");
            }

            // Retrieve a Semantic Kernel instance for this document process
            _sk = await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);

            // Extract custom metadata (if any)
            var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);
            var customDataString = documentMetaData?.MetadataJson ?? "No custom data available for this query";

            // Get the Document ID from the metadata
            var generatedDocumentId = sectionContentNode?.AssociatedGeneratedDocumentId;

            // Create a user-facing name for the section
            var fullSectionName = string.IsNullOrEmpty(sectionOrTitleNumber)
                ? sectionOrTitleText
                : $"{sectionOrTitleNumber} {sectionOrTitleText}";

            // Use a smaller subset of source docs for your prompt
            var sourceDocumentsWithHighestScoringPartitions = sourceDocuments
                .OrderByDescending(d => d.GetHighestScoringPartitionFromCitations())
                .Take(5)
                .ToList();

            // Build the example string from the top doc partitions
            var sectionExample = new StringBuilder();
            foreach (var document in sourceDocumentsWithHighestScoringPartitions)
            {
                sectionExample.AppendLine("[EXAMPLE: Document Extract]");
                sectionExample.AppendLine(document.FullTextOutput);
                sectionExample.AppendLine("[/EXAMPLE]");
                _createdBodyContentNode.ContentNodeSystemItem?.SourceReferences.Add(document);
            }

            var exampleString = sectionExample.ToString();
            var mainPrompt = await BuildMainPrompt(
                numberOfPasses: "2",  // Just an example
                fullSectionName: fullSectionName,
                customDataString: customDataString,
                tableOfContentsString: tableOfContentsString,
                exampleString: exampleString,
                promptInstructions: sectionContentNode?.PromptInstructions
            );

            // Create our plugin for storing / retrieving partial content
            _contentStatePlugin = new ContentStatePlugin(exampleString, InitialBlockSize);

            // Create our Document History plugin for tracking content
            _documentHistoryPlugin = new DocumentHistoryPlugin(_dbContext, _mapper, generatedDocumentId, sectionContentNode!);

            // Set up the group chat with two agents: ContentAgent & ReviewerAgent
            _agentGroupChat = await SetupAgentsAsync(documentProcess, fullSectionName, mainPrompt, customDataString);

            // Kick off the conversation
            const string initialMessage = "Please begin drafting the content.";
            _agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, initialMessage));

            // Loop the conversation as long as needed
            var cancellationToken = new CancellationToken(false);
            await foreach (var message in _agentGroupChat.InvokeAsync(cancellationToken))
            {
                ProcessAgentMessage(message); 
            }

            // Gather any additional references
            var executionId = Guid.Parse(_executionIdString);
            var sourceReferenceItems = _pluginSourceReferenceCollector.GetAll(executionId);
            if (sourceReferenceItems.Count > 0)
            {
                _createdBodyContentNode.ContentNodeSystemItem!.SourceReferences.AddRange(sourceReferenceItems);
            }
            _pluginSourceReferenceCollector.Clear(executionId);

            // Final content
            var finalContent = _contentStatePlugin.GetAssembledContent();
            if (!string.IsNullOrEmpty(finalContent))
            {
                // Remove placeholders or bracket tags if needed
                finalContent = finalContent.Replace("[*COMPLETE*]", string.Empty);
                _createdBodyContentNode.Text = finalContent;
                _createdBodyContentNode.GenerationState = ContentNodeGenerationState.Completed;
            }

            return new List<ContentNode> { _createdBodyContentNode };
        }

        /// <summary>
        /// Minimal processing if needed (e.g. logging).
        /// You can parse content here if you want to store partial results in ContentStatePlugin,
        /// but this example defers that to direct function calls within the conversation (see instructions).
        /// </summary>
        private void ProcessAgentMessage(ChatMessageContent message)
        {
            _logger.LogInformation($"Message from {message.AuthorName}");
            _logger.LogInformation(message.Content);

            // Example: if you want to detect a "complete" marker from the reviewer
            if (message.Content.Contains("[COMPLETE]", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Received COMPLETION signal from ReviewerAgent.");
            }
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
            // Suppose you have a "SectionGenerationMainPrompt" in your DB or config
            var mainPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync(
                "SectionGenerationMainPrompt",
                ProcessName
            );
            var mainPromptTemplate = mainPromptInfo.Text; // or fallback

            // We'll add something like a fallback if none found:
            if (string.IsNullOrWhiteSpace(mainPromptTemplate))
            {
                mainPromptTemplate = @"
This is a placeholder prompt for {{ fullSectionName }}. 
Pass count: {{ numberOfPasses }}
[EXAMPLE_CONTENT]
{{ exampleString }}
[/EXAMPLE_CONTENT]
";
            }

            // Add optional instructions
            string sectionSpecificPromptInstructions = "";
            if (!string.IsNullOrEmpty(promptInstructions))
            {
                sectionSpecificPromptInstructions = $@"
[SECTIONPROMPTINSTRUCTIONS]
{promptInstructions}
[/SECTIONPROMPTINSTRUCTIONS]
";
            }

            var template = Template.Parse(mainPromptTemplate);
            var result = await template.RenderAsync(new
            {
                numberOfPasses,
                fullSectionName,
                customDataString,
                tableOfContentsString,
                exampleString,
                documentProcessName = ProcessName,
                sectionSpecificPromptInstructions
            }, member => member.Name);

            return result;
        }

        /// <summary>
        /// Sets up our two agents: ContentAgent (merged writer+knowledge) and ReviewerAgent.
        /// We also provide simpler instructions to each agent.
        /// </summary>
        private async Task<AgentGroupChat> SetupAgentsAsync(DocumentProcessInfo documentProcess, string fullSectionName,
            string mainPrompt,
            string customDataString)
        {
            // Add our plugins to the kernel
            _sk.ImportPluginFromObject(_contentStatePlugin, "ContentState");
            _sk.ImportPluginFromObject(_documentHistoryPlugin, "DocumentHistory");

            // Add execution ID to the kernel for tracking plugin invocations
            _sk.Data.Add("System-ExecutionId", _executionIdString);

            var loggerFactory = _sp.GetRequiredService<ILoggerFactory>();

            var agentGroupChat = new AgentGroupChat
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    // We can use a simpler selection strategy
                    SelectionStrategy = new RoundRobinAgentSelectionStrategy(_logger),
                    TerminationStrategy = new CompleteTagTerminationStrategy()
                }
            };

            // 1) ContentAgent: does both writing + knowledge retrieval
            var contentAgentPromptExecutionSettings =
                await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess,
                    AiTaskType.ContentGeneration);

            var contentAgent = new ChatCompletionAgent
            {
                Name = "ContentAgent",
                Instructions = BuildContentAgentInstructions(fullSectionName, mainPrompt, customDataString),
                Kernel = _sk,
                Arguments = new KernelArguments(contentAgentPromptExecutionSettings),
                LoggerFactory = loggerFactory
            };

            // 2) ReviewerAgent: checks content, potentially signals [COMPLETE]
            var reviewerAgentPromptExecutionSettings =
                await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess,
                    AiTaskType.ContentGeneration);

            var reviewerAgent = new ChatCompletionAgent
            {
                Name = "ReviewerAgent",
                Instructions = BuildReviewerAgentInstructions(fullSectionName),
                Kernel = _sk.Clone(), // Or you can keep the same
                Arguments = new KernelArguments(reviewerAgentPromptExecutionSettings),
                LoggerFactory = loggerFactory
            };

            agentGroupChat.AddAgent(contentAgent);
            agentGroupChat.AddAgent(reviewerAgent);

            return agentGroupChat;
        }

        /// <summary>
        /// Example system prompt instructions for the merged ContentAgent.
        /// Both writing + knowledge retrieval responsibilities live here.
        /// </summary>
        private string BuildContentAgentInstructions(
            string fullSectionName,
            string mainPrompt,
            string customDataString)
        {
            // Synthesize a simpler instruction block:
            return $@"
You are the ContentAgent, responsible for:
1) Drafting the content for the section: {fullSectionName}
2) Retrieving any additional knowledge needed from available tools/plugins (e.g., searching for data).
3) Storing partial content blocks by calling the ContentState plugin functions:
   - StoreSequenceContent(sequenceNumber, content)
   - etc.

Follow these guidelines:
- If you have partial content to store, call StoreSequenceContent(...) rather than printing bracket tags.
- If you need to see the partial content, call GetAssembledContent() or GetSequenceWithContext().
- If you want to incorporate or revise something, you can store again to override an existing sequence.
- Use the knowledge retrieval function (like 'SearchKnowledgeBase' if available) for additional context instead of guessing.
- Always Use StoreSequenceContent(...) to save your content.
- Include the sequence number(s) in your message if you have stored content you'd like the reviewer to see.
- Use GetNextSequenceNumber() to get the next available sequence number for appending content.
- When adding (or revising) sequences, don't repeat section name in later sections, add (continued) or similar. The content will be assembled in order.
- Use the DocumentHistory plugin to look at previously written sections to maintain consistency and avoid duplication.

Below is your main prompt context:
[MAIN_PROMPT]
{mainPrompt}
[/MAIN_PROMPT]

You can also rely on custom data for this project:
[METADATA]
{customDataString}
[/METADATA]

When you produce content that you think is ready for review, end your message with something like 'Ready for review.'
You do NOT have to add any special tags for the reviewer to pick up. The conversation flow will handle it.

Remember:
- Keep the content relevant to the section: {fullSectionName}
- Avoid duplications or referencing other sections not relevant to this one
- If done, you can say 'I've completed this section' or similar. The reviewer will then finalize with a [COMPLETE] if everything is good.
";
        }

        /// <summary>
        /// Example system prompt instructions for the ReviewerAgent.
        /// Minimal instructions for reviewing, possibly finalizing the content with [COMPLETE].
        /// </summary>
        private string BuildReviewerAgentInstructions(string fullSectionName)
        {
            return $@"
You are the ReviewerAgent, responsible for:
1) Reviewing the content provided by the ContentAgent.
2) Checking correctness, style, coverage, etc.
3) Optionally calling the ContentState plugin to read the partial or final content using GetAssembledContent() or GetSequenceContent(...) 
   if you need to see what's been stored so far.
4) If content is incorrect or has flaws, request the ContentAgent to revise it. Reference the sequence number.
5) If you find the content incomplete or too short, ask the ContentAgent to continue adding more content. 
6) If the content looks good, signal completion by responding only with:
[COMPLETE]

Rules:
- Keep your instructions concise.
- If you see minor issues, ask ContentAgent to fix them.
- If you see major issues, ask ContentAgent to revise and mention what's missing or incorrect.
- If everything is good, output [COMPLETE] to end.
- [DETAIL: ...] tags are expected in the produced content - these are indicators for human editors to fill in details. Don't ask for their removal.
- Remove any placeholder tags like '[*COMPLETE*]' or similar.
- Focus only on the current section: {fullSectionName} 
- Make sure there is no repetition across sequences, and especially make sure there are no (continued) or similar tags in the final content. 
  Don't repeat section headlines several times.
- Use the DocumentHistory plugin to look at previously written sections to maintain consistency and avoid duplication.
";
        }

        /// <summary>
        /// Initialize an empty content node to store results.
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
                ParentId = sectionContentNode?.ParentId,
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
    }
}
