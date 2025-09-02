// Copyright (c) Microsoft Corporation. All rights reserved.
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
using System.Text.Json;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Extensions;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic
{
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

    /// <inheritdoc />
    public class AgentAiCompletionService : IAiCompletionService
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly string _processName;
        private readonly DocGenerationDbContext _dbContext;
        private readonly ILogger<AgentAiCompletionService> _logger;
        private readonly IServiceProvider _sp;
        private Kernel? _sk;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IPluginSourceReferenceCollector _pluginSourceReferenceCollector;
        private readonly IPromptInfoService _promptInfoService;
        private readonly IMapper _mapper;
        private const int InitialBlockSize = 100;

        private readonly string _executionIdString;
        private ContentNode _createdBodyContentNode = default!;
        private AgentGroupChat? _agentGroupChat;
        private ContentStatePlugin? _contentStatePlugin;
        private DocumentHistoryPlugin? _documentHistoryPlugin;
        private readonly IKernelFactory _kernelFactory;

        /// <summary>
        /// Constructs an instance of the AgentAiCompletionService.
        /// </summary>
        /// <param name="dbContextFactory"></param>
        /// <param name="parameters"></param>
        /// <param name="processName"></param>
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
            _processName = processName;
            _mapper = _sp.GetRequiredService<IMapper>();
            _executionIdString = Guid.NewGuid().ToString();
            _pluginSourceReferenceCollector = _sp.GetRequiredService<IPluginSourceReferenceCollector>();
        }

        /// <inheritdoc />
        public async Task<List<ContentNode>> GetBodyContentNodes(
            List<SourceReferenceItem> sourceReferences,
            string sectionOrTitleNumber,
            string sectionOrTitleText,
            ContentNodeType contentNodeType,
            string tableOfContentsString,
            Guid? metadataId,
            ContentNode? sectionContentNode)
        {
            // Prepare an empty ContentNode for the final text
            InitializeContentNode(sectionContentNode);

            var documentProcess = await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(_processName);
            if (documentProcess == null)
            {
                throw new InvalidOperationException($"Document process '{_processName}' not found.");
            }

            // Retrieve a Semantic Kernel instance for this document process, carrying ProviderSubjectId when available
            var providerSubjectId = UserExecutionContext.ProviderSubjectId;
            _sk = !string.IsNullOrWhiteSpace(providerSubjectId)
                ? await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess, providerSubjectId)
                : await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);

            // Extract custom metadata (if any)
            var documentMetaData = await _dbContext.DocumentMetadata.FindAsync(metadataId);
            var customDataString = documentMetaData?.MetadataJson ?? "No custom data available for this query";

            // Get the Document ID from the metadata
            var generatedDocumentId = sectionContentNode?.AssociatedGeneratedDocumentId;

            // Create a user-facing name for the section
            var fullSectionName = string.IsNullOrEmpty(sectionOrTitleNumber)
                ? sectionOrTitleText
                : $"{sectionOrTitleNumber} {sectionOrTitleText}";

            // Use a smaller subset of source refs for your prompt (Kernel Memory, Vector Store, etc.)
            var sourceDocumentsWithHighestScoringPartitions = sourceReferences
                .OrderByDescending(d => d.GetHighestRelevanceScore())
                .Take(5)
                .ToList();

            // Build the example string from the top doc partitions
            var sectionExample = new StringBuilder();
            foreach (var document in sourceDocumentsWithHighestScoringPartitions)
            {
                sectionExample.AppendLine("[EXAMPLE: Document Extract]");
                // Select example text based on source type
                var exampleText = document switch
                {
                    KernelMemoryDocumentSourceReferenceItem km => km.FullTextOutput,
                    VectorStoreAggregatedSourceReferenceItem vs => string.Join("", vs.Chunks.Select(c => c.Text)),
                    _ => string.Empty
                };
                if (!string.IsNullOrWhiteSpace(exampleText))
                {
                    sectionExample.AppendLine(exampleText);
                }
                sectionExample.AppendLine("[/EXAMPLE]");
                _createdBodyContentNode.ContentNodeSystemItem!.SourceReferences.Add(document);
            }

            var exampleString = sectionExample.ToString();

            // Create our plugin for storing / retrieving partial content
            var grainFactory = _sp.GetRequiredService<IGrainFactory>();
            var executionId = Guid.Parse(_executionIdString);
            _contentStatePlugin = new ContentStatePlugin(grainFactory, executionId, exampleString, InitialBlockSize);
            await _contentStatePlugin.InitializeAsync();

            // Create our Document History plugin for tracking content
            _documentHistoryPlugin = new DocumentHistoryPlugin(_dbContext, _mapper, generatedDocumentId, sectionContentNode!);

            // Set up the group chat with multiple agents (builds the main prompt inside)
            _agentGroupChat = await SetupMultiAgentGroupChatAsync(documentProcess, fullSectionName, customDataString, exampleString, tableOfContentsString, sectionContentNode);

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
            var sourceReferenceItems = _pluginSourceReferenceCollector.GetAll(executionId);
            if (sourceReferenceItems.Count > 0)
            {
                _createdBodyContentNode.ContentNodeSystemItem!.SourceReferences.AddRange(sourceReferenceItems);
            }
            _pluginSourceReferenceCollector.Clear(executionId);

            // Final content
            var finalContent = await _contentStatePlugin.GetAssembledContent();
            if (!string.IsNullOrEmpty(finalContent))
            {
                // Remove placeholders or bracket tags if needed
                finalContent = finalContent.Replace("[*COMPLETE*]", string.Empty);
                _createdBodyContentNode.Text = finalContent;
                _createdBodyContentNode.GenerationState = ContentNodeGenerationState.Completed;
            }

            // Cleanup: clear and deactivate the ContentStateGrain
            var contentStateGrain = grainFactory.GetGrain<IContentStateGrain>(executionId);
            await contentStateGrain.ClearAndDeactivateAsync();

            return new List<ContentNode> { _createdBodyContentNode };
        }

        /// <summary>
        /// Minimal processing if needed (e.g. logging).
        /// You can parse content here if you want to store partial results in ContentStatePlugin,
        /// but this example defers that to direct function calls within the conversation (see instructions).
        /// </summary>
        private void ProcessAgentMessage(ChatMessageContent message)
        {
            try
            {
                _logger.LogInformation($"Message from {message.AuthorName}");
                if (!string.IsNullOrEmpty(message.Content))
                {
                    _logger.LogInformation(message.Content);
                }

                // Example: if you want to detect a "complete" marker from the reviewer
                if (!string.IsNullOrEmpty(message.Content) && message.Content.Contains("[COMPLETE]", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Received COMPLETION signal from ReviewerAgent.");
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "JSON parsing error while processing agent message, continuing with conversation");
                // Continue processing despite JSON errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing agent message");
                // Don't rethrow to avoid breaking the conversation flow
            }
        }

        /// <summary>
        /// Builds a Scriban-based agentic main prompt for the ContentAgent.
        /// </summary>
        private async Task<string> BuildAgenticMainPrompt(
            string numberOfPasses,
            string fullSectionName,
            string customDataString,
            string tableOfContentsString,
            string exampleString,
            string? promptInstructions,
            string pluginFunctionDescriptions,
            string documentProcessName)
        {
            // Use SectionGenerationAgenticMainPrompt
            var mainPromptInfo = await _promptInfoService.GetPromptByShortCodeAndProcessNameAsync(
                PromptNames.SectionGenerationAgenticMainPrompt,
                documentProcessName
            );
            var mainPromptTemplate = mainPromptInfo?.Text;

            // Fallback if not found
            if (string.IsNullOrWhiteSpace(mainPromptTemplate))
            {
                mainPromptTemplate = @"You are the ContentAgent for section {{ fullSectionName }}.";
            }

            string sectionSpecificPromptInstructions = string.Empty;
            if (!string.IsNullOrEmpty(promptInstructions))
            {
                sectionSpecificPromptInstructions = $@"\n[SECTIONPROMPTINSTRUCTIONS]\n{promptInstructions}\n[/SECTIONPROMPTINSTRUCTIONS]\n";
            }

            // Render the template
            var template = Template.Parse(mainPromptTemplate);
            var result = await template.RenderAsync(new
            {
                numberOfPasses,
                fullSectionName,
                customDataString,
                tableOfContentsString,
                exampleString,
                documentProcessName,
                sectionSpecificPromptInstructions,
                pluginFunctionDescriptions
            }, member => member.Name);

            return result;
        }

        /// <summary>
        /// Sets up a multi-agent group chat with orchestration and plugin selection.
        /// </summary>
        private async Task<AgentGroupChat> SetupMultiAgentGroupChatAsync(
            DocumentProcessInfo documentProcess,
            string fullSectionName,
            string customDataString,
            string exampleString,
            string tableOfContentsString,
            ContentNode? sectionContentNode)
        {
            var loggerFactory = _sp.GetRequiredService<ILoggerFactory>();

            // Prepare plugins from the base kernel
            var availablePlugins = new Dictionary<string, object>();
            if (_sk is null)
            {
                throw new InvalidOperationException("Kernel was not initialized before setting up agents.");
            }

            foreach (var plugin in _sk.Plugins)
            {
                if (plugin.Name == "DefaultKernelPlugin") continue; // Skip the default plugin
                availablePlugins[plugin.Name] = plugin;
            }
            // Add ContentState and DocumentHistory plugins
            availablePlugins["ContentState"] = _contentStatePlugin!;
            availablePlugins["DocumentHistory"] = _documentHistoryPlugin!;

            // --- Extract plugin and function descriptions for ContentAgent prompt ---
            var pluginFunctionDescriptions = new StringBuilder();
            foreach (var plugin in _sk.Plugins)
            {
                pluginFunctionDescriptions.AppendLine($"Plugin: {plugin.Name}");
                foreach (var function in plugin.GetFunctionsMetadata())
                {
                    pluginFunctionDescriptions.AppendLine($"  - {function.Name}: {function.Description}");
                }
            }

            // Build non-orchestrator agent configurations first
            var agentConfigs = new List<AgentConfiguration>
            {
                new AgentConfiguration
                {
                    Name = "ContentAgent",
                    Description = "The ContentAgent is responsible for drafting, expanding, and revising content for the section. It should be invoked whenever new content is needed, or when the ReviewerAgent requests changes or additional detail.",
                    Instructions = await BuildAgenticMainPrompt(
                        numberOfPasses: "6",
                        fullSectionName: fullSectionName,
                        customDataString: customDataString,
                        tableOfContentsString: tableOfContentsString,
                        exampleString: exampleString,
                        promptInstructions: sectionContentNode?.PromptInstructions, // use actual prompt instructions if present
                        pluginFunctionDescriptions: pluginFunctionDescriptions.ToString(),
                        documentProcessName: _processName
                    ),
                    AllowedPlugins = availablePlugins.Keys.ToList(),
                    IsOrchestrator = false,
                    HasTerminationAuthority = false
                },
                new AgentConfiguration
                {
                    Name = "ReviewerAgent",
                    Description = "The ReviewerAgent reviews content for correctness, style, coverage, and completeness. It should be invoked after the ContentAgent produces or revises content, and can request further changes or signal completion.",
                    Instructions = BuildReviewerAgentInstructions(fullSectionName, exampleString, customDataString),
                    AllowedPlugins = ["ContentState", "DocumentHistory"],
                    IsOrchestrator = false
                    // HasTerminationAuthority intentionally omitted so only OrchestratorAgent can terminate
                },
                new AgentConfiguration
                {
                    Name = "ResearcherAgent",
                    Description = "The ResearcherAgent is responsible for investigating [DETAIL: ...] tags in the content. It uses all available plugins and tools to attempt to fill in missing details, and reports any findings back to the agent chat. If no additional information is found, it signals that the detail tag should remain.",
                    Instructions = BuildResearcherAgentInstructions(fullSectionName, exampleString, customDataString, pluginFunctionDescriptions.ToString()),
                    AllowedPlugins = availablePlugins.Keys.ToList(),
                    IsOrchestrator = false,
                    HasTerminationAuthority = false
                }
            };

            // Generate agent summary string for orchestrator instructions
            var agentSummary = string.Join("\n", agentConfigs.Select(a => $"- {a.Name}: {a.Description}"));

            // Now add the OrchestratorAgent, injecting the agent summary
            agentConfigs.Add(
                new AgentConfiguration
                {
                    Name = "OrchestratorAgent",
                    Description = "The OrchestratorAgent manages the workflow and delegates tasks to other agents based on their roles and the current conversation context. It should analyze the last message and the agent descriptions to determine which agent is best suited to act next. The OrchestratorAgent is responsible for ensuring the workflow progresses efficiently and that each agent is used according to its expertise. When forwarding review or feedback information, always retain and forward any specific instructions or details about particular content sequences or sections, so that agents (especially ContentAgent) can act on precise feedback (e.g., if a message says 'in Sequence 400, this and that is missing, please add it', ensure this information is preserved in your message to the next agent). Do not summarize away or omit such details.",
                    Instructions = $"""
                                    You are the OrchestratorAgent. Your job is to manage the workflow between all agents for section: {fullSectionName}.

                                    You have access to the following agents and their roles:
                                    {agentSummary}

                                    Workflow Guidance:
                                    - After content is drafted or revised, route to the agent whose description best matches the next required action.
                                    - If content needs review, route to ReviewerAgent.
                                    - If ReviewerAgent requests changes, route back to ContentAgent with clear instructions based on the reviewer's feedback.
                                    - If ReviewerAgent requests additional background or detail for [DETAIL: ...] tags, route to ResearcherAgent to investigate and attempt to fill in those tags using all available plugins/tools.
                                    - If ResearcherAgent finds new information, route back to ReviewerAgent or ContentAgent as appropriate to update the content.
                                    - If ResearcherAgent cannot find additional information, signal that the detail tag should remain.
                                    - If ReviewerAgent signals completion with [COMPLETE], end the process.
                                    - Always analyze the last message and agent descriptions to decide the next step.
                                    - Do not allow agents to repeat the same content; ensure progress is made in each cycle.
                                    - If an agent is satisfied, do not route back to the same agent unless further action is required.
                                    - When forwarding review or feedback information, always retain and forward any specific instructions or details about particular content sequences or sections, so that agents (especially ContentAgent) can act on precise feedback (e.g., if a message says 'in Sequence 400, this and that is missing, please add it', ensure this information is preserved in your message to the next agent). Do not summarize away or omit such details.

                                    CRITICAL:
                                    - You MUST forward all feedback, especially detail tag updates or requests from the ReviewerAgent, verbatim to the next agent. Do NOT summarize, paraphrase, or alter this feedback in any way.
                                    - You MUST preserve the names and locations of all [DETAIL: ...] tags exactly as received throughout the workflow. Do NOT change, rename, or omit any detail tag or its context.
                                    - It is the responsibility of the ContentAgent or ReviewerAgent to incorporate or alter the content to fit purpose, not yours.
                                    - If you receive a message containing detail tag updates, requests, or feedback, copy the relevant content verbatim into your message to the next agent.
                                    - Never summarize, paraphrase, or omit any information about detail tags or their names/locations.
                                    - When the section is truly finished and no further action is needed, you MUST output the tag [COMPLETE] as your only response. This is the only way to signal the system to end the conversation.

                                    Example responses:
                                    - [NEXT:ReviewerAgent] Please review the new content.
                                    - [NEXT:ContentAgent] The reviewer requested more detail on X and a revision of Y.
                                    - [NEXT:ResearcherAgent] Please investigate the [DETAIL: ...] tags and provide any additional information you can find.
                                    - [NEXT:ReviewerAgent] ResearcherAgent has provided new information for the detail tags. Please review.
                                    - [NEXT:ContentAgent] ResearcherAgent has provided new information. Please update the content accordingly.
                                    - [NEXT:ReviewerAgent] Content has been revised as requested. Please review again.
                                    - [COMPLETE]
                                    """,
                    AllowedPlugins = [],
                    IsOrchestrator = true,
                    HasTerminationAuthority = true
                }
            );

            // Create agent instances
            var agents = new List<ChatCompletionAgent>();
            foreach (var config in agentConfigs)
            {
                // Each agent gets its own kernel instance
                var psid = UserExecutionContext.ProviderSubjectId;
                var agentKernel = !string.IsNullOrWhiteSpace(psid)
                    ? await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess, psid)
                    : await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);
                agentKernel.Data.Add("System-ExecutionId", _executionIdString);

                // Clear all plugins before adding only the allowed ones
                agentKernel.Plugins.Clear();

                // Import allowed plugins
                foreach (var pluginName in config.AllowedPlugins)
                {
                    if (pluginName == "DefaultKernelPlugin") continue; // Skip the default plugin
                    if (availablePlugins.TryGetValue(pluginName, out object? pluginObj))
                    {
                        if (pluginObj is KernelPlugin kernelPluginObject)
                        {
                            agentKernel.Plugins.Add((kernelPluginObject));
                        }
                        else
                        {
                            agentKernel.ImportPluginFromObject(pluginObj, pluginName);
                        }
                    }
                }

                var promptSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.ContentGeneration);

                agents.Add(new ChatCompletionAgent
                {
                    Name = config.Name,
                    Instructions = config.Instructions,
                    Kernel = agentKernel,
                    Arguments = new KernelArguments(promptSettings),
                    LoggerFactory = loggerFactory
                });
            }

            var selectionStrategy = new MultiAgentSelectionStrategy(_logger, orchestratorAgentName: "OrchestratorAgent");
            var terminationStrategy = new MultiAgentTerminationStrategy(_logger, ["[COMPLETE]"], agentConfigs.Where(a => a.HasTerminationAuthority).Select(a => a.Name));

            var agentGroupChat = new AgentGroupChat
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    SelectionStrategy = selectionStrategy,
                    TerminationStrategy = terminationStrategy
                }
            };

            foreach (var agent in agents)
            {
                agentGroupChat.AddAgent(agent);
            }

            return agentGroupChat;
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

        /// <summary>
        /// Prompt instructions for the ReviewerAgent.
        /// Enhanced to require explicit assessment of [DETAIL: ...] tags and always request ResearcherAgent lookup if present.
        /// </summary>
        private string BuildReviewerAgentInstructions(string fullSectionName, string exampleString, string customDataString)
        {
            return $@"
You are the ReviewerAgent, responsible for:
1) Critically reviewing the content provided by the ContentAgent for correctness, style, structure, coverage, and—most importantly—alignment with the types of information and requirements present in the provided source documents and metadata.
2) Use the ContentStatePlugin to:
   - Get all sequence numbers with GetSequenceNumbers().
   - Review each sequence with GetSequenceContent(sequenceNumber).
   - Review the full assembled content with GetAssembledContent().
3) For each sequence, check for completeness, correctness, and alignment with requirements. Reference specific sequence numbers in your feedback.
4) If content is missing or incomplete, specify what additional sequences are needed and instruct the ContentAgent to add them.
5) For every [DETAIL: ...] tag found in the content, you MUST explicitly request the OrchestratorAgent to route to the ResearcherAgent to attempt to expand or fill in the detail, unless the ResearcherAgent has already confirmed no further information is available for that tag.
6) If any [DETAIL: ...] tags remain after the ResearcherAgent has confirmed no further information is available, do not require further revision. Instead, instruct the ContentAgent to add a [HUMAN-INTERVENTION] block at the very end of the content, describing what a human needs to do for each unresolved detail tag. After this, you may mark the section as complete.
7) Do NOT update or modify content directly; only the ContentAgent should do this. Your role is to review and request changes.
8) Compare the content to the following source documents and metadata to ensure all relevant information and requirements are addressed.

[SOURCE_DOCUMENTS]
{exampleString}
[/SOURCE_DOCUMENTS]

[METADATA]
{customDataString}
[/METADATA]

9) If and only if the content is comprehensive, well-structured, and fully aligned with the project requirements and source document types, and all [DETAIL: ...] tags have been assessed by the ResearcherAgent (with [HUMAN-INTERVENTION] block added if needed), signal completion by responding only with:
[COMPLETE]

Rules:
- Be highly critical and analytical. Do not accept vague, incomplete, or poorly structured content.
- If you see minor issues, ask ContentAgent to fix them, referencing the sequence number.
- If you see major issues, ask ContentAgent to revise and mention what's missing or incorrect, referencing the sequence number.
- If you see a [DETAIL: ...] tag, always request a lookup by the ResearcherAgent unless it has already been confirmed as unexpandable.
- If [DETAIL: ...] tags remain after ResearcherAgent confirmation, instruct ContentAgent to add a [HUMAN-INTERVENTION] block at the end of the content, summarizing what a human needs to do for each unresolved tag.
- If everything is good, output [COMPLETE] to end, but only if all [DETAIL: ...] tags have been assessed by the ResearcherAgent and [HUMAN-INTERVENTION] block is present if needed.
- Remove any placeholder tags like '[*COMPLETE*]' or similar.
- Focus only on the current section: {fullSectionName}
- Make sure there is no repetition across sequences, and especially make sure there are no (continued) or similar tags in the final content.
  Don't repeat section headlines several times.
- Use the DocumentHistory plugin to look at previously written sections to maintain consistency and avoid duplication.
- If you are unsure, err on the side of requesting revision.

Tool Call Reference:
- ContentStatePlugin.GetSequenceNumbers(): Returns a list of all sequence numbers.
- ContentStatePlugin.GetSequenceContent(sequenceNumber): Returns the content for a specific sequence.
- ContentStatePlugin.GetAssembledContent(): Returns the full content assembled in order.
";
        }

        /// <summary>
        /// Prompt instructions for the ResearcherAgent.
        /// Guides the agent to look for [DETAIL: ...] tags and use all available plugins/tools to fill them in.
        /// </summary>
        private string BuildResearcherAgentInstructions(string fullSectionName, string exampleString, string customDataString, string pluginFunctionDescriptions)
        {
            return $@"
You are the ResearcherAgent. Your job is to investigate all [DETAIL: ...] tags in the content for section: {fullSectionName}.

Instructions:
- For each [DETAIL: ...] tag found in the content, you MUST always consider and attempt to use all available plugins and tools listed below to fill in the missing detail, enhance, verify, or supplement information wherever possible.
- Retain the original [DETAIL: ...] tag name/description in the content so that other agents can find the tag when they review your findings.
- For any other opportunity to enhance or verify information in the section, also consider and attempt to use all available plugins/tools.
- If you find relevant information, report it back to the agent chat, referencing the specific detail tag and sequence number if possible.
- If you cannot find additional information using any available tools, indicate that the detail tag should remain for human editors.
- Do not modify the content directly; only report findings and recommendations.
- Use the following plugins and functions through Tool Calling. 
  Prioritize their use over general knowledge or assumptions when they are relevant to the detail tag or content being reviewed.

{pluginFunctionDescriptions}

[EXAMPLES]
{exampleString}
[/EXAMPLES]

[METADATA]
{customDataString}
[/METADATA]
";
        }
    }
}
