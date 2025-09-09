using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Review.Contracts;
using Microsoft.Greenlight.Grains.Shared.Contracts.Models;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.Review;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.ContentReference;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
// Removed legacy ReviewKernelMemoryRepository dependency
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Orleans.Concurrency;
using Scriban;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

namespace Microsoft.Greenlight.Grains.Review
{
    [StatelessWorker]
    public class ReviewQuestionAnswererGrain : Grain, IReviewQuestionAnswererGrain
    {
        private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
        private readonly ILogger<ReviewQuestionAnswererGrain> _logger;
        private readonly IMapper _mapper;
        private readonly IKernelFactory _kernelFactory;
        private readonly IRagContextBuilder _ragContextBuilder;
        private readonly IDocumentProcessInfoService _documentProcessInfoService;
        private readonly IContentReferenceService _contentReferenceService;
        private readonly IPromptInfoService _promptInfoService;
        private readonly IServiceProvider _sp;

        public ReviewQuestionAnswererGrain(
            IDbContextFactory<DocGenerationDbContext> dbContextFactory,
            ILogger<ReviewQuestionAnswererGrain> logger,
            IKernelFactory kernelFactory,
            IRagContextBuilder ragContextBuilder,
            IDocumentProcessInfoService documentProcessInfoService,
            IContentReferenceService contentReferenceService,
            IPromptInfoService promptInfoService,
            IMapper mapper,
            IServiceProvider sp)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
            _kernelFactory = kernelFactory;
            _ragContextBuilder = ragContextBuilder;
            _documentProcessInfoService = documentProcessInfoService;
            _contentReferenceService = contentReferenceService;
            _promptInfoService = promptInfoService;
            _mapper = mapper;
            _sp = sp;
        }

        public async Task<GenericResult> AnswerQuestionAsync(Guid reviewInstanceId, ReviewQuestionInfo question)
        {
            try
            {
                _logger.LogInformation("Answering question {QuestionId} for review instance {ReviewInstanceId}",
                    question.Id, reviewInstanceId);

                await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

                // Get the review instance to determine document process
                var reviewInstance = await dbContext.ReviewInstances
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == reviewInstanceId);

                if (reviewInstance == null)
                {
                    _logger.LogError("Review instance {ReviewInstanceId} not found", reviewInstanceId);
                    return GenericResult.Failure($"Review instance {reviewInstanceId} not found");
                }

                string? documentProcessShortName = reviewInstance.DocumentProcessShortName;
                string answer;

                // Try to get content reference items for this review
                var contentReferenceItems = await GetReviewContentReferenceItemsAsync(reviewInstanceId);

                if (contentReferenceItems.Any())
                {
                    _logger.LogInformation("Using agentic process for review {ReviewInstanceId} with {Count} reference items",
                        reviewInstanceId, contentReferenceItems.Count);

                    if (documentProcessShortName is null)
                    {
                        _logger.LogWarning("Review instance {ReviewInstanceId} has no DocumentProcessShortName; cannot answer agentically.", reviewInstanceId);
                        answer = "No process context available to answer this question.";
                    }
                    else
                    {
                        answer = await AnswerUsingAgenticProcessAsync(reviewInstanceId, question, documentProcessShortName, contentReferenceItems);
                    }
                }
                else
                {
                    // No content references found: report insufficient context (legacy KM fallback removed)
                    _logger.LogWarning("No content references for review {ReviewInstanceId}; returning NOT_FOUND placeholder", reviewInstanceId);
                    answer = "No context available to answer this question at this time.";
                }

                var answerModel = new ReviewQuestionAnswer()
                {
                    OriginalReviewQuestionId = question.Id,
                    FullAiAnswer = answer,
                    ReviewInstanceId = reviewInstanceId,
                    OriginalReviewQuestionText = question.Question,
                    OriginalReviewQuestionType = question.QuestionType,
                    Order = question.Order,
                    CreatedUtc = question.CreatedUtc != default ? question.CreatedUtc : DateTime.UtcNow
                };

                // If the question has been answered before for this instance, delete it
                var existingAnswer = await dbContext.ReviewQuestionAnswers
                    .FirstOrDefaultAsync(x => x.OriginalReviewQuestionId == question.Id &&
                                             x.ReviewInstanceId == reviewInstanceId);

                if (existingAnswer != null)
                {
                    dbContext.ReviewQuestionAnswers.Remove(existingAnswer);
                }

                dbContext.ReviewQuestionAnswers.Add(answerModel);
                await dbContext.SaveChangesAsync();

                // Notify orchestration grain that a question has been answered
                var orchestrationGrain = GrainFactory.GetGrain<IReviewExecutionOrchestrationGrain>(reviewInstanceId);
                await orchestrationGrain.OnQuestionAnsweredAsync(answerModel.Id);

                return GenericResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error answering question {QuestionId} for review instance {ReviewInstanceId}",
                    question.Id, reviewInstanceId);
                return GenericResult.Failure($"Failed to answer question {question.Id}: {ex.Message}");
            }
        }

        private async Task<List<ContentReferenceItem>> GetReviewContentReferenceItemsAsync(Guid reviewInstanceId)
        {
            // Use ContentReferenceService to get review content references (handles RAG text population, etc.)
            return await _contentReferenceService.GetReviewContentReferenceItemsAsync(reviewInstanceId);
        }

        private async Task<string> AnswerUsingAgenticProcessAsync(
            Guid reviewInstanceId,
            ReviewQuestionInfo question,
            string documentProcessShortName,
            List<ContentReferenceItem> contentReferences)
        {
            // Get document process info
            var documentProcess = !string.IsNullOrEmpty(documentProcessShortName)
                ? await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync(documentProcessShortName)
                : await _documentProcessInfoService.GetDocumentProcessInfoByShortNameAsync("Default");

            if (documentProcess == null)
            {
                _logger.LogError("Document process {DocumentProcessName} not found, using fallback approach", documentProcessShortName);
                throw new Exception($"Document process not found for review question answering");
            }

            // Prepare kernel and plugins with per-user context when available
            string? providerSubjectId = null;
            try
            {
                var orchestrator = GrainFactory.GetGrain<IReviewExecutionOrchestrationGrain>(reviewInstanceId);
                var state = await orchestrator.GetStateAsync();
                providerSubjectId = state?.StartedByProviderSubjectId;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to fetch ProviderSubjectId from orchestration for review {ReviewInstanceId}", reviewInstanceId);
            }

            return await UserContextRunner.RunAsync(providerSubjectId, async () =>
            {
                var kernel = !string.IsNullOrWhiteSpace(providerSubjectId)
                    ? await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess, providerSubjectId)
                    : await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);
                var promptExecutionSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.QuestionAnswering);
                var kernelArguments = new KernelArguments(promptExecutionSettings);

                // Build availablePlugins dictionary
                var availablePlugins = new Dictionary<string, object>();
                foreach (var plugin in kernel.Plugins)
                {
                    if (plugin.Name == "DefaultKernelPlugin") continue;
                    availablePlugins[plugin.Name] = plugin;
                }
                // Add ContentReferenceLookupPlugin and ContentStatePlugin
                var vectorProvider = _sp.GetService(typeof(ISemanticKernelVectorStoreProvider)) as ISemanticKernelVectorStoreProvider
                    ?? throw new InvalidOperationException("ISemanticKernelVectorStoreProvider not available");
                var contentReferenceLookupPlugin = new ContentReferenceLookupPlugin(_contentReferenceService, _ragContextBuilder, vectorProvider);
                var grainFactory = _sp.GetService(typeof(IGrainFactory)) as IGrainFactory;
                var executionId = Guid.NewGuid();
                if (grainFactory is null) throw new InvalidOperationException("GrainFactory not initialized");
                var contentStatePlugin = new ContentStatePlugin(grainFactory, executionId, string.Empty, 100);
                await contentStatePlugin.InitializeAsync();
                availablePlugins["ContentReferenceLookupPlugin"] = contentReferenceLookupPlugin;
                availablePlugins["ContentStatePlugin"] = contentStatePlugin;

                // Build context using RAG
                var maxReferences = documentProcess.NumberOfCitationsToGetFromRepository;
                string ragContext = await _ragContextBuilder.BuildContextWithSelectedReferencesAsync(
                    question.Question,
                    contentReferences,
                    topN: maxReferences);

                // Prepare reference IDs for plugin use
                var referenceIds = contentReferences.Select(r => r.Id).ToList();

                // Build the prompt using Scriban to inject context and reference IDs
                string? promptTemplate;
                if (documentProcessShortName == null)
                    throw new InvalidOperationException("DocumentProcessShortName missing for prompt retrieval.");
                string dpShort = documentProcessShortName; // non-null by contract
                if (question.QuestionType == ReviewQuestionType.Requirement)
                {
                    var tmp = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                        nameof(DefaultPromptCatalogTypes.ReviewRequirementAnswerPrompt), dpShort);
                    promptTemplate = tmp ?? string.Empty;
                }
                else
                {
                    var tmp = await _promptInfoService.GetPromptTextByShortCodeAndProcessNameAsync(
                        nameof(DefaultPromptCatalogTypes.ReviewQuestionAnswerPrompt), dpShort);
                    promptTemplate = tmp ?? string.Empty;
                }
                var scribanTemplate = Template.Parse(promptTemplate);
                var renderedPrompt = await scribanTemplate.RenderAsync(new
                {
                    context = ragContext,
                    question = question.Question,
                    referenceIds = referenceIds
                }, member => member.Name);

                var documentProcessNameInstruction = $"You're working on a document belonging to the document process '{documentProcessShortName}'.\n";
                var currentDocumentInstruction = string.Empty;
                if (contentReferences.Any()) // Check if there are any content references
                {
                    currentDocumentInstruction = "The primary document(s) you are reviewing are listed below with their Content Reference ID(s):\n";
                    currentDocumentInstruction += string.Join("\n", contentReferences.Select(r => $"- Name: '{r.DisplayName}', ID: '{r.Id}'-"));
                    currentDocumentInstruction += "\nWhen searching within these specific document(s), use the ContentReferenceLookupPlugin.SearchSimilarChunks function, passing the appropriate ID.\n";
                }

                var combinedInstructions = documentProcessNameInstruction + currentDocumentInstruction + "\n";

                // --- Agent configuration setup ---
                var agentConfigs = new List<AgentConfiguration>
            {
                new AgentConfiguration
                {
                    Name = "QuestionAnswererAgent",
                    Description = "The QuestionAnswererAgent answers the review question using available plugins and context.",
                    Instructions = $"""
You are the QuestionAnswererAgent. Your job is to draft, revise, and finalize the answer to the review question using all available context, references, and plugins.

{combinedInstructions}

====================
OBJECTIVES
====================
- Produce a complete, correct, and well-supported answer to the review question.
- Incorporate all relevant context, references, and feedback from the ReviewerAgent (via the OrchestratorAgent).
- Only you are allowed to modify the assembled answer using ContentStatePlugin.

====================
TOOL USAGE
====================
- Use ContentStatePlugin.GetSequenceNumbers() to see all sequence numbers in use.
- Use ContentStatePlugin.GetNextSequenceNumber() to determine the next available sequence number.
- Use ContentStatePlugin.StoreSequenceContent(sequenceNumber, content) to store or update each block of your answer.
- Use ContentStatePlugin.GetAssembledContent() to review the current full answer.
- If you revise or expand your answer, update the relevant sequence(s) using StoreSequenceContent.
- Always refer to supporting evidence as "the document" or by its filename, NOT by reference ID.

====================
ANSWER CREATION PROCESS
====================
1. Review the review question, context, and references provided.
2. Draft your answer in logical blocks, storing each with ContentStatePlugin.StoreSequenceContent.
3. Regularly check the assembled answer with ContentStatePlugin.GetAssembledContent().
4. If you receive feedback or revision requests from the ReviewerAgent (via the OrchestratorAgent), update your answer accordingly.
5. If the context is insufficient, explicitly state what is missing and which document(s) or file(s) were searched.
6. If the answer requires connecting information from different parts of the document or external libraries, attempt to do so and explain your reasoning.
7. Repeat drafting and revision until the answer is complete and ready for review.
8. Structure your answer with headings for Findings, Discussion and Conclusion. 
   Use MarkDown heading level 1 for these headlines ( # )
   In the Conclusion, be conclusive and state whether or not the question or requirement has been answered or fulfilled.
9. After your conclusion, include a list of references used to support your answer. Use the format "Reference: [filename or description] (sequence number)".
10. Use tables and lists where appropriate. ENSURE tables are valid markdown. Ensure everything is valid markdown, but focus especially on tables as they are prone to formatting issues.
11. Don't refer to the document being reviewed as "the context". Use the filename or description of the document, or just "the document".

====================
CRITICAL RULES
====================
- Only you may update the answer content using ContentStatePlugin. The ReviewerAgent and ResearcherAgent must not modify the answer.
- Do not mark the process as complete; only the OrchestratorAgent may do so.

{renderedPrompt}
""",
                    AllowedPlugins = [
                        "ContentReferenceLookupPlugin", "ContentStatePlugin",
                        "DP__DocumentLibraryPlugin", "DP__KMDocsPlugin"
                    ],
                    IsOrchestrator = false,
                    HasTerminationAuthority = false
                },
                new AgentConfiguration
                {
                    Name = "ReviewerAgent",
                    Description = "The ReviewerAgent reviews the answer for completeness, correctness, and alignment with the question and context.",
                    Instructions = $"""
You are the ReviewerAgent. Your job is to critically review the answer provided by the QuestionAnswererAgent.

{combinedInstructions}

====================
OBJECTIVES
====================
- Ensure the answer is complete, correct, and fully aligned with the review question and provided context.
- All feedback and requests for changes must be routed through the OrchestratorAgent to the QuestionAnswererAgent.
- Only the QuestionAnswererAgent is allowed to modify the assembled answer using ContentStatePlugin. You must never replace or overwrite the answer yourself, except for final cleanup (see below).
- The ResearcherAgent must never modify or replace the assembled answer. If you observe this, instruct the OrchestratorAgent to have the QuestionAnswererAgent restore or revise the answer as appropriate.

====================
TOOL USAGE
====================
- Always use ContentStatePlugin.GetAssembledContent() to review the current answer (except for answers from the ResearcherAgent). Do not rely solely on chat messages to look at the content.
- Use ContentStatePlugin.GetSequenceNumbers() and ContentStatePlugin.GetSequenceContent(sequenceNumber) to review all parts and versions of the answer.
- If several versions of the content are available, use the most recent/last one for your review and for final cleanup.

====================
REVIEW PROCESS
====================
- If the answer is incomplete, unclear, incorrect, or not fully supported by the provided context/references, provide actionable feedback and request revision, referencing specific sequence numbers. Route this feedback through the OrchestratorAgent to the QuestionAnswererAgent.
- You MUST route to the ResearcherAgent in any of these specific situations:
  1. When the answer mentions that a search was only provided in a limited portion of the document but other parts may cover it.
  2. When the answer contains phrases like "limited context", "provided data", "partial analysis", "restricted scope", or suggests needing a more thorough search.
  3. When the question or answer specifically refers to regulations, laws, or supporting documents outside the current document that need validation.
  4. When the answer only uses a small part of the document but the question seems to require broader coverage.
  5. When the answer makes claims that aren't substantiated by the provided context.
- The findings from the ResearcherAgent are not added to the answer directly by the ResearcherAgent. Instead, you should instruct the OrchestratorAgent to have the QuestionAnswererAgent revise the answer as appropriate if you deem its findings relevant and necessary for the answer.
- If the answer requires connecting information from different parts of the document or external libraries, ensure the QuestionAnswererAgent or ResearcherAgent has attempted to do so. If not, instruct them to do so.
- If you are unsure, err on the side of requesting revision.
- Do NOT modify the answer with the ContentStatePlugin during the review process (except for final cleanup as instructed below). 
  Instead, if you have a request for a change, notify the OrchestrationAgent to ask the QuestionAnswererAgent to implement the change.
- If you receive additional references from the ResearcherAgent, please make sure to notify the QuestionAnswererAgent about this so
  they can be incorporated in the reference list for the answer.

====================
INTERACTING WITH RESEARCHERAGENT
====================
When instructing the OrchestratorAgent to route to the ResearcherAgent, be specific about what you need:

- For limited context or search scope issues: "Please have the ResearcherAgent perform a more comprehensive search of the entire document regarding [specific topic/claim] to verify the completeness of the answer."

- For regulatory or external sources: "Please have the ResearcherAgent verify the regulatory requirements mentioned in sequence X using DP__DocumentLibraryPlugin and other available tools/plugins to search for relevant regulations or supporting documentation."

- For unsubstantiated claims: "Please have the ResearcherAgent investigate the claim in sequence X about [specific claim] which appears to lack supporting evidence in the context."

- For claims referencing external evidence: "Please have the ResearcherAgent verify the external claims in sequence X using all available sources to ensure all points are substantiated."

====================
FINAL CLEANUP
====================
- When you deem the answer to be complete and ready for acceptance, you MUST clean up the assembled content by removing any agent instructions and agent tags before final acceptance. Use the ContentStatePlugin to perform this cleanup on the most recent/last version.
- This is the only time you are allowed to modify the assembled answer directly. You MUST NOT modify the answer during the review process, except for this final cleanup step.

====================
CRITICAL RULES
====================
- Never replace or overwrite the answer with your own considerations or findings. Only the QuestionAnswererAgent may update the answer content.
- Do NOT use the [COMPLETE] marker; only the OrchestratorAgent is allowed to mark the process as complete.
""",
                    AllowedPlugins = ["ContentStatePlugin"],
                    IsOrchestrator = false,
                    HasTerminationAuthority = false
                },
                new AgentConfiguration
                {
                    Name = "ResearcherAgent",
                    Description = "The ResearcherAgent assists with fact-finding and cross-referencing using available plugins.",
                    Instructions = $"""
You are the ResearcherAgent. Your job is to assist with fact-finding, cross-referencing, and providing background for the review question and its answer.

{combinedInstructions}

====================
OBJECTIVES
====================
- Provide relevant evidence, background, or cross-references to support the review question and answer.
- Use all available plugins and tools to find supporting information, clarify uncertainties, or provide regulatory/background context as needed.
- Do NOT modify or replace the assembled answer. Only the QuestionAnswererAgent may update the answer content.

====================
TOOL USAGE
====================
- Use ContentReferenceLookupPlugin.SearchSimilarChunks with the relevant Content Reference ID to search within the current document(s).
- Use DP__DocumentLibraryPlugin and DP__KMDocsPlugin to search for or answer questions from supporting documentation or similar documents, respectively.
- Use ContentStatePlugin.GetSequenceNumbers(), ContentStatePlugin.GetSequenceContent(sequenceNumber), and ContentStatePlugin.GetAssembledContent() to review the answer and its parts, but NOT to modify it.
- Use ContentStatePlugin.GetAssembledContent() to review the current answer. Do not rely solely on chat messages or context.

====================
RESEARCH PROCESS
====================
- When asked, search for additional facts, evidence, or background as requested by the ReviewerAgent or QuestionAnswererAgent (via the OrchestratorAgent).
- Report your findings, referencing the specific aspect or content, and which document or file (by filename or description) and sequence number(s) were used.
- If you cannot find additional information, indicate that no further information is available and which documents/files and sequence numbers were searched.
- Do NOT add your findings directly to the answer. Instead, report them so the QuestionAnswererAgent can incorporate them if needed.

====================
CRITICAL RULES
====================
- Never modify or replace the assembled answer. Only the QuestionAnswererAgent may update the answer content.
- Do NOT use the [COMPLETE] marker; only the OrchestratorAgent is allowed to mark the process as complete.
""",
                    AllowedPlugins = availablePlugins.Keys.ToList(),
                    IsOrchestrator = false,
                    HasTerminationAuthority = false
                }
            };

                // --- Build agent summary string for orchestrator instructions ---
                var agentSummary = string.Join("\n", agentConfigs.Select(a => $"- {a.Name}: {a.Description}"));

                // --- Add OrchestratorAgent with injected agent summary ---
                agentConfigs.Add(
                new AgentConfiguration
                {
                    Name = "OrchestratorAgent",
                    Description = "The OrchestratorAgent manages the workflow and delegates tasks to the other agents.",
                    Instructions = $"""
You are the OrchestratorAgent. Your job is to manage the workflow and delegate tasks to the other agents for answering the review question: "{question.Question}".

{combinedInstructions}

====================
OTHER AGENT'S ROLES
====================
{agentSummary}

====================
WORKFLOW GUIDANCE
====================
- The process begins with the QuestionAnswererAgent drafting an answer.
- After an answer is produced, it is your responsibility to route it to ReviewerAgent for review.
- If ReviewerAgent requests changes, route back to QuestionAnswererAgent with the reviewer's feedback.
- If ReviewerAgent requests additional facts or cross-references, route to ResearcherAgent to investigate and provide findings using ContentReferenceLookupPlugin.
- If ResearcherAgent provides new information, route back to ReviewerAgent or QuestionAnswererAgent as appropriate to update or review the answer.
- If ResearcherAgent cannot find additional information, inform ReviewerAgent so they can decide if the answer is sufficient or requires human intervention.
- Always analyze the last message and agent descriptions to decide the next step.
- Do not allow agents to repeat the same content; ensure progress is made in each cycle.
- If an agent is satisfied, do not route back to the same agent unless further action is required.
- When forwarding review or feedback information, always retain and forward any specific instructions or details about particular content or references, so that agents can act on precise feedback. Do not summarize away or omit such details.

====================
CRITICAL RULES
====================
- You MUST forward all feedback, especially requests or findings from the ReviewerAgent or ResearcherAgent, verbatim to the next agent. Do NOT summarize, paraphrase, or alter this feedback in any way. You must never omit, shorten, or cut any part of the feedback or requests when routing between agents. If the feedback is long, you must forward the entire content, even if it is lengthy or repetitive.
- It is the responsibility of the QuestionAnswererAgent or ReviewerAgent to incorporate or alter the answer to fit purpose, not yours.
- If you receive a message containing requests or feedback, copy the relevant content verbatim into your message to the next agent.
- Never summarize, paraphrase, or omit any information about requests or findings.
- You MUST NOT emit [COMPLETE] unless ALL of the following are true:
    * The ReviewerAgent has explicitly confirmed that the answer is comprehensive, well-supported, and all cross-references or background have been checked as needed.
    * The ReviewerAgent has NOT requested any further changes, revisions, or updates.
    * There are NO outstanding requests, revisions, or feedback from any agent.
    * The answer is truly final and complete.
- If there are any outstanding changes, revisions, or requests (from ReviewerAgent or otherwise), you MUST NOT emit [COMPLETE] under any circumstances. Instead, continue routing the conversation until all issues are resolved and the ReviewerAgent explicitly signals completion.
- NEVER use [COMPLETE] in a message that also says the answer is not complete, or that further work is required. If you need to state that the answer is not complete, do NOT include [COMPLETE] anywhere in your message.
- [COMPLETE] must only be used as a standalone marker when the process is truly finished.

====================
EXAMPLE RESPONSES
====================
- [NEXT:ReviewerAgent] Please review the answer.
- [NEXT:QuestionAnswererAgent] The reviewer requested clarification on X and a revision of Y.
- [NEXT:ResearcherAgent] Please investigate the following aspect and provide any additional information you can find using ContentReferenceLookupPlugin.
- [NEXT:ReviewerAgent] ResearcherAgent has provided new information. Please review.
- [NEXT:QuestionAnswererAgent] ResearcherAgent has provided new information. Please update the answer accordingly.
- [NEXT:ReviewerAgent] Answer has been revised as requested. Please review again.

- [NEXT:<AgentName>] <Instruction for Agent> (if you want to route to a specific agent not listed in the examples)

- [COMPLETE]   (ONLY if the answer is truly final, with no outstanding changes or requests, and your message does not state that the answer is incomplete or further work is needed)
""",
                    AllowedPlugins = ["ContentStatePlugin"],
                    IsOrchestrator = true,
                    HasTerminationAuthority = true
                }
            );

                // --- Create agent group chat ---
                var loggerFactory = _sp.GetService(typeof(ILoggerFactory)) as ILoggerFactory;
                var agents = new List<ChatCompletionAgent>();
                foreach (var config in agentConfigs)
                {
                    var agentKernel = !string.IsNullOrWhiteSpace(providerSubjectId)
                        ? await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess, providerSubjectId)
                        : await _kernelFactory.GetKernelForDocumentProcessAsync(documentProcess);
                    agentKernel.Data.Add("System-ExecutionId", executionId.ToString());
                    agentKernel.Plugins.Clear();
                    foreach (var pluginName in config.AllowedPlugins)
                    {
                        if (availablePlugins.TryGetValue(pluginName, out var pluginObj))
                        {
                            if (pluginObj is KernelPlugin kernelPluginObj)
                                agentKernel.Plugins.Add(kernelPluginObj);
                            else
                                agentKernel.ImportPluginFromObject(pluginObj, pluginName);
                        }
                    }
                    var promptSettings = await _kernelFactory.GetPromptExecutionSettingsForDocumentProcessAsync(documentProcess, AiTaskType.QuestionAnswering);
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
                // Kick off the conversation
                agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, "Please answer the review question."));
                var cancellationToken = new System.Threading.CancellationToken(false);
                await foreach (var message in agentGroupChat.InvokeAsync(cancellationToken))
                {
                    _logger.LogInformation($"Agentic message: {message.AuthorName}: {message.Content}");
                    // No manual [COMPLETE] check here; only OrchestratorAgent with termination authority can end the chat.
                }
                // Get the answer from ContentStatePlugin
                var finalAnswer = await contentStatePlugin.GetAssembledContent();
                return finalAnswer;
            });
        }
    }
}
