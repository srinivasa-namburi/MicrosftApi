// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Chat.Agentic;
using Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Grains.Document.Contracts;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using Orleans.Concurrency;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

namespace Microsoft.Greenlight.Grains.Chat;

/// <summary>
/// Agentic Flow Task execution grain using multi-agent orchestration for conversational requirement gathering.
/// Uses AgentGroupChat with 4 specialized agents: Conversation, RequirementCollector, Validation, and Orchestrator.
/// </summary>
[Reentrant]
public class FlowTaskAgenticExecutionGrain : Grain, IFlowTaskAgenticExecutionGrain
{
    private readonly IPersistentState<FlowTaskGrainState> _state;
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IKernelFactory _kernelFactory;
    private readonly ISystemPromptInfoService _promptService;
    private readonly IGrainFactory _grainFactory;
    private readonly IMapper _mapper;
    private readonly ILogger<FlowTaskAgenticExecutionGrain> _logger;

    // Agent group chat components
    private AgentGroupChat? _agentGroupChat;
    private FlowTaskTemplateDetailDto? _templateDto;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskAgenticExecutionGrain"/> class.
    /// </summary>
    public FlowTaskAgenticExecutionGrain(
        [PersistentState("flowTaskAgentic")]
        IPersistentState<FlowTaskGrainState> state,
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IKernelFactory kernelFactory,
        ISystemPromptInfoService promptService,
        IGrainFactory grainFactory,
        IMapper mapper,
        ILogger<FlowTaskAgenticExecutionGrain> logger)
    {
        _state = state;
        _dbContextFactory = dbContextFactory;
        _kernelFactory = kernelFactory;
        _promptService = promptService;
        _grainFactory = grainFactory;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<FlowTaskExecutionResult> StartExecutionAsync(
        Guid flowSessionId,
        Guid templateId,
        string initialMessage,
        string userContext)
    {
        try
        {
            // Check if already started
            if (_state.State.CurrentState != FlowTaskExecutionState.NotStarted)
            {
                _logger.LogWarning("Attempted to start already-running agentic FlowTask in state {State}", _state.State.CurrentState);
                return new FlowTaskExecutionResult
                {
                    IsSuccess = false,
                    ResponseMessage = "Flow Task has already been started.",
                    State = _state.State.CurrentState
                };
            }

            // Load template from database
            _templateDto = await LoadTemplateAsync(templateId);
            if (_templateDto == null)
            {
                return new FlowTaskExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"Template {templateId} not found",
                    State = FlowTaskExecutionState.Failed
                };
            }

            // Initialize grain state
            _state.State.TemplateId = templateId;
            _state.State.FlowSessionId = flowSessionId;
            _state.State.CurrentState = FlowTaskExecutionState.CollectingRequirements;
            _state.State.StartedAtUtc = DateTime.UtcNow;
            await _state.WriteStateAsync();

            // Initialize state grain
            var executionId = this.GetPrimaryKey();
            var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(executionId);
            await stateGrain.InitializeAsync(_templateDto);

            // Create FlowTaskStatePlugin (shared across agents that need it)
            var flowStatePlugin = new FlowTaskStatePlugin(_grainFactory, executionId);

            // Create the 5 specialized agents using the proven AgentGroupChat pattern
            var agents = new List<ChatCompletionAgent>();

            // ConversationAgent - talks to the user
            var conversationAgent = await CreateConversationAgentAsync(flowStatePlugin);
            agents.Add(conversationAgent);

            // RequirementCollectorAgent - extracts values from user responses
            var requirementCollectorAgent = await CreateRequirementCollectorAgentAsync(flowStatePlugin);
            agents.Add(requirementCollectorAgent);

            // ValidationAgent - checks if all requirements are met
            var validationAgent = await CreateValidationAgentAsync(flowStatePlugin);
            agents.Add(validationAgent);

            // ConfirmationAgent - detects user confirmation intent using natural language understanding
            var confirmationAgent = await CreateConfirmationAgentAsync();
            agents.Add(confirmationAgent);

            // OrchestratorAgent - manages workflow and emits [COMPLETE]
            var orchestratorAgent = await CreateOrchestratorAgentAsync();
            agents.Add(orchestratorAgent);

            // Create selection and termination strategies
            // Use custom FlowTaskAgentSelectionStrategy that routes based on execution state
            var selectionStrategy = new FlowTaskAgentSelectionStrategy(
                _logger,
                getCurrentState: () => _state.State.CurrentState,
                orchestratorAgentName: "OrchestratorAgent");

            var terminationStrategy = new MultiAgentTerminationStrategy(_logger, terminationTags: new[] { "[COMPLETE]" }, agentsWithTerminationAuthority: new[] { "OrchestratorAgent" });

            // Create AgentGroupChat with strategies
            _agentGroupChat = new AgentGroupChat
            {
                ExecutionSettings = new AgentGroupChatSettings
                {
                    SelectionStrategy = selectionStrategy,
                    TerminationStrategy = terminationStrategy
                }
            };

            foreach (var agent in agents)
            {
                _agentGroupChat.AddAgent(agent);
            }

            _logger.LogInformation("Started agentic Flow Task execution {ExecutionId} for template {TemplateId}", executionId, templateId);

            // Kick off the conversation with the user's initial message
            var taskDescription = $"""
                User context: {userContext}

                Template: {_templateDto.DisplayName}
                Description: {_templateDto.Description}

                Initial user message: {initialMessage}

                Your goal: Collect all required information conversationally and naturally.
                """;

            _logger.LogInformation("Starting agent group chat with task description (length: {Length})", taskDescription.Length);
            _agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, taskDescription));

            // Invoke the chat and collect first response
            var cancellationToken = new CancellationToken();
            string? firstResponse = null;

            await foreach (var message in _agentGroupChat.InvokeAsync(cancellationToken))
            {
                _logger.LogInformation("Agent {AgentName}: {Content}", message.AuthorName, message.Content);

                // Capture the first ConversationAgent response to return to the user
                if (firstResponse == null && message.AuthorName == "ConversationAgent")
                {
                    firstResponse = message.Content;
                    break; // Stop after first ConversationAgent response for this initial invocation
                }
            }

            _logger.LogInformation("Got first response from chat (length: {Length})", firstResponse?.Length ?? 0);

            return new FlowTaskExecutionResult
            {
                ExecutionId = executionId,
                State = FlowTaskExecutionState.CollectingRequirements,
                ResponseMessage = firstResponse ?? "Flow Task started. Let's gather the information we need.",
                IsComplete = false,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting agentic Flow Task execution");
            _state.State.CurrentState = FlowTaskExecutionState.Failed;
            await _state.WriteStateAsync();

            return new FlowTaskExecutionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                State = FlowTaskExecutionState.Failed
            };
        }
    }

    /// <inheritdoc />
    public async Task<FlowTaskExecutionResult> ProcessMessageAsync(string message)
    {
        try
        {
            // Validate state - accept messages in CollectingRequirements and AwaitingConfirmation states
            if (_state.State.CurrentState != FlowTaskExecutionState.CollectingRequirements &&
                _state.State.CurrentState != FlowTaskExecutionState.AwaitingConfirmation)
            {
                _logger.LogWarning("Attempted to process message in invalid state {State}", _state.State.CurrentState);

                // Provide user-friendly error messages with recovery guidance
                string userMessage = _state.State.CurrentState switch
                {
                    FlowTaskExecutionState.ExecutingOutputs =>
                        "Your request is currently being processed. Please wait a moment for it to complete.",

                    FlowTaskExecutionState.Completed =>
                        "This Flow Task has already been completed. If you'd like to create another, please start a new Flow Task.",

                    FlowTaskExecutionState.Failed =>
                        "This Flow Task encountered an error and cannot continue. Please start a new Flow Task to try again.",

                    FlowTaskExecutionState.ValidatingRequirements =>
                        "Your information is currently being validated. Please wait a moment.",

                    _ =>
                        $"Cannot process your message at this time (state: {_state.State.CurrentState}). Please try starting a new Flow Task."
                };

                return new FlowTaskExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = userMessage,
                    ResponseMessage = userMessage,
                    State = _state.State.CurrentState
                };
            }

            if (_agentGroupChat == null)
            {
                _logger.LogError("Agent group chat not initialized");
                const string errorMessage = "This Flow Task was not properly initialized. Please start a new Flow Task.";
                return new FlowTaskExecutionResult
                {
                    IsSuccess = false,
                    ErrorMessage = errorMessage,
                    ResponseMessage = errorMessage,
                    State = FlowTaskExecutionState.Failed
                };
            }

            // Add user message to the chat
            // The FlowTaskAgentSelectionStrategy will automatically route based on execution state
            _agentGroupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, message));

            // Invoke the chat and collect response
            var cancellationToken = CancellationToken.None;
            string? responseText = null;
            bool readyToExecute = false;
            bool readyForConfirmation = false;
            string? previousAgentName = null;

            await foreach (var msg in _agentGroupChat.InvokeAsync(cancellationToken))
            {
                _logger.LogInformation("Agent {AgentName}: {Content}", msg.AuthorName, msg.Content);

                // Capture ConversationAgent responses to return to the user
                if (msg.AuthorName == "ConversationAgent")
                {
                    if (responseText == null)
                    {
                        responseText = msg.Content;
                    }

                    // Break if ConversationAgent was routed to by Orchestrator (final message to user after orchestration)
                    // This allows the full agent workflow to execute before returning to the user
                    if (previousAgentName == "OrchestratorAgent")
                    {
                        _logger.LogInformation("ConversationAgent provided final response after orchestration, breaking loop");
                        break;
                    }
                }

                // Check if ConversationAgent or Orchestrator signaled ready for confirmation
                if (msg.Content?.Contains("[READY_FOR_CONFIRMATION]") == true)
                {
                    readyForConfirmation = true;
                    _logger.LogInformation("Agent signaled READY_FOR_CONFIRMATION");
                }

                // Check if OrchestratorAgent signaled ready to execute outputs
                if (msg.AuthorName == "OrchestratorAgent" && msg.Content?.Contains("[EXECUTE_OUTPUTS]") == true)
                {
                    readyToExecute = true;
                    _logger.LogInformation("OrchestratorAgent signaled EXECUTE_OUTPUTS");
                    break;
                }

                previousAgentName = msg.AuthorName;
            }

            // Transition to AwaitingConfirmation if ConversationAgent presented summary
            if (readyForConfirmation && _state.State.CurrentState == FlowTaskExecutionState.CollectingRequirements)
            {
                _state.State.CurrentState = FlowTaskExecutionState.AwaitingConfirmation;
                await _state.WriteStateAsync(cancellationToken);
                _logger.LogInformation("Transitioned to AwaitingConfirmation state");
            }

            if (readyToExecute)
            {
                _logger.LogInformation("Agentic Flow Task execution {ExecutionId} ready to execute outputs", this.GetPrimaryKey());

                // Execute outputs before marking as completed
                _state.State.CurrentState = FlowTaskExecutionState.ExecutingOutputs;
                await _state.WriteStateAsync(cancellationToken);

                var outputResult = await ExecuteOutputsAsync();

                if (outputResult.IsSuccess)
                {
                    _state.State.CurrentState = FlowTaskExecutionState.Completed;
                    _state.State.CompletedAtUtc = DateTime.UtcNow;
                    await _state.WriteStateAsync(cancellationToken);

                    _logger.LogInformation("Agentic Flow Task execution {ExecutionId} completed successfully with {OutputCount} outputs",
                        this.GetPrimaryKey(), outputResult.Outputs.Count);

                    // Format the output results into a user-friendly message
                    var outputMessage = FormatOutputsForUser(outputResult);
                    responseText = $"{responseText}\n\n{outputMessage}";

                    // Add internal assistant message to notify Orchestrator that outputs have been executed
                    // [NODISPLAY] tag prevents this message from appearing in the UI
                    _agentGroupChat.AddChatMessage(new ChatMessageContent(
                        AuthorRole.Assistant,
                        "[NODISPLAY][OUTPUTS_EXECUTED] Flow Task outputs have been successfully generated and the task is now complete.[/NODISPLAY]"));

                    // Continue the conversation so Orchestrator can respond with [COMPLETE]
                    _logger.LogInformation("Outputs executed successfully, invoking chat for Orchestrator to emit [COMPLETE]");
                    await foreach (var finalMsg in _agentGroupChat.InvokeAsync(cancellationToken))
                    {
                        _logger.LogInformation("Final agent {AgentName}: {Content}", finalMsg.AuthorName, finalMsg.Content);
                        // Termination strategy will detect [COMPLETE] and stop the chat
                        // Selection strategy will route assistant message → Orchestrator (default case)
                    }
                }
                else
                {
                    var errorMessages = string.Join("; ", outputResult.Errors.Select(e => $"{e.OutputName}: {e.ErrorMessage}"));
                    _logger.LogError("Failed to execute outputs for Flow Task {ExecutionId}: {ErrorMessages}",
                        this.GetPrimaryKey(), errorMessages);
                    _state.State.CurrentState = FlowTaskExecutionState.Failed;
                    await _state.WriteStateAsync(cancellationToken);
                    responseText = $"{responseText}\n\nI encountered an error while generating the outputs: {errorMessages}";

                    // Add internal assistant message for failure case
                    // [NODISPLAY] tag prevents this message from appearing in the UI
                    _agentGroupChat.AddChatMessage(new ChatMessageContent(
                        AuthorRole.Assistant,
                        $"[NODISPLAY][OUTPUTS_FAILED] Flow Task output execution failed: {errorMessages}[/NODISPLAY]"));

                    // Invoke chat for Orchestrator to emit [COMPLETE] on failure
                    _logger.LogInformation("Outputs failed, invoking chat for Orchestrator to emit [COMPLETE]");
                    await foreach (var finalMsg in _agentGroupChat.InvokeAsync(cancellationToken))
                    {
                        _logger.LogInformation("Final agent {AgentName}: {Content}", finalMsg.AuthorName, finalMsg.Content);
                        // Termination strategy will detect [COMPLETE] and stop the chat
                    }
                }
            }

            return new FlowTaskExecutionResult
            {
                ExecutionId = this.GetPrimaryKey(),
                State = _state.State.CurrentState,
                ResponseMessage = responseText ?? "Processing your response...",
                IsComplete = _state.State.CurrentState == FlowTaskExecutionState.Completed || _state.State.CurrentState == FlowTaskExecutionState.Failed,
                IsSuccess = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message in agentic Flow Task execution");
            return new FlowTaskExecutionResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                State = _state.State.CurrentState
            };
        }
    }

    /// <inheritdoc />
    public Task<FlowTaskExecutionState> GetCurrentStateAsync()
    {
        return Task.FromResult(_state.State.CurrentState);
    }

    /// <inheritdoc />
    public Task<bool> IsCompleteAsync()
    {
        return Task.FromResult(_state.State.CurrentState == FlowTaskExecutionState.Completed);
    }

    /// <inheritdoc />
    public async Task CancelAsync()
    {
        _state.State.CurrentState = FlowTaskExecutionState.Cancelled;
        _state.State.CompletedAtUtc = DateTime.UtcNow;
        await _state.WriteStateAsync();

        _logger.LogInformation("Cancelled agentic Flow Task execution {ExecutionId}", this.GetPrimaryKey());
    }

    /// <inheritdoc />
    public async Task<FlowTaskExecutionSummary> GetSummaryAsync()
    {
        var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(this.GetPrimaryKey());
        var collectedValues = await stateGrain.GetCollectedValuesAsync();

        return new FlowTaskExecutionSummary
        {
            ExecutionId = this.GetPrimaryKey(),
            TemplateId = _state.State.TemplateId,
            State = _state.State.CurrentState,
            StartedAt = _state.State.StartedAtUtc ?? DateTime.UtcNow,
            CompletedAt = _state.State.CompletedAtUtc,
            CollectedValues = collectedValues
        };
    }

    /// <inheritdoc />
    public async Task<FlowTaskValidationResult> ValidateRequirementsAsync()
    {
        var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(this.GetPrimaryKey());
        var pendingRequired = await stateGrain.GetPendingRequiredFieldsAsync();

        var isValid = string.IsNullOrWhiteSpace(pendingRequired) || pendingRequired == "[]";

        return new FlowTaskValidationResult
        {
            IsValid = isValid,
            MissingRequiredFields = isValid ? new List<string>() : System.Text.Json.JsonSerializer.Deserialize<List<string>>(pendingRequired) ?? new List<string>()
        };
    }

    /// <inheritdoc />
    public async Task<FlowTaskOutputResult> ExecuteOutputsAsync()
    {
        try
        {
            _logger.LogInformation("Executing outputs for agentic FlowTask {ExecutionId}", this.GetPrimaryKey());

            // Validate that we're in a state where outputs can be executed
            if (_state.State.CurrentState != FlowTaskExecutionState.ExecutingOutputs)
            {
                _logger.LogWarning("Cannot execute outputs - FlowTask is in state {State}", _state.State.CurrentState);
                return new FlowTaskOutputResult
                {
                    IsSuccess = false,
                    Errors = new List<FlowTaskOutputError>
                    {
                        new FlowTaskOutputError
                        {
                            OutputName = "All",
                            ErrorMessage = $"Cannot execute outputs in state {_state.State.CurrentState}"
                        }
                    }
                };
            }

            // Load template if not already loaded
            if (_templateDto == null)
            {
                _templateDto = await LoadTemplateAsync(_state.State.TemplateId);
                if (_templateDto == null)
                {
                    return new FlowTaskOutputResult
                    {
                        IsSuccess = false,
                        Errors = new List<FlowTaskOutputError>
                        {
                            new FlowTaskOutputError
                            {
                                OutputName = "All",
                                ErrorMessage = "Template not found"
                            }
                        }
                    };
                }
            }

            var outputs = new List<FlowTaskOutput>();

            // Execute each output template in order
            if (_templateDto.OutputTemplates != null)
            {
                foreach (var outputTemplate in _templateDto.OutputTemplates.OrderBy(o => o.ExecutionOrder))
                {
                    try
                    {
                        _logger.LogInformation("Executing output template {TemplateName} of type {OutputType}",
                            outputTemplate.Name, outputTemplate.OutputType);

                        FlowTaskOutput output;

                        switch (outputTemplate.OutputType)
                        {
                            case "McpToolInvocation":
                                // TODO: Implement MCP tool invocation in Story #375
                                output = new FlowTaskOutput
                                {
                                    Name = outputTemplate.Name,
                                    Type = "McpToolInvocation",
                                    Content = "MCP tool invocation not yet implemented (Story #375)",
                                    ContentType = "text/plain"
                                };
                                break;

                            case "TextSummary":
                                // Generate summary using collected values
                                output = await GenerateTextSummaryAsync(outputTemplate);
                                break;

                            case "DocumentGeneration":
                                // Generate document using collected values
                                output = await GenerateDocumentAsync(outputTemplate);
                                break;

                            default:
                                _logger.LogWarning("Unknown output type {OutputType} for template {TemplateName}",
                                    outputTemplate.OutputType, outputTemplate.Name);
                                output = new FlowTaskOutput
                                {
                                    Name = outputTemplate.Name,
                                    Type = outputTemplate.OutputType,
                                    Content = "Output type not supported",
                                    ContentType = "text/plain"
                                };
                                break;
                        }

                        outputs.Add(output);

                        _logger.LogInformation("Executed output template {TemplateName}", outputTemplate.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing output template {TemplateName}", outputTemplate.Name);
                        return new FlowTaskOutputResult
                        {
                            IsSuccess = false,
                            Errors = new List<FlowTaskOutputError>
                            {
                                new FlowTaskOutputError
                                {
                                    OutputName = outputTemplate.Name,
                                    ErrorMessage = ex.Message
                                }
                            }
                        };
                    }
                }
            }

            _logger.LogInformation("FlowTask {GrainId} executed {OutputCount} outputs", this.GetPrimaryKey(), outputs.Count);

            return new FlowTaskOutputResult
            {
                IsSuccess = true,
                Outputs = outputs
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing outputs for FlowTask {GrainId}", this.GetPrimaryKey());
            return new FlowTaskOutputResult
            {
                IsSuccess = false,
                Errors = new List<FlowTaskOutputError>
                {
                    new FlowTaskOutputError
                    {
                        OutputName = "All",
                        ErrorMessage = ex.Message
                    }
                }
            };
        }
    }

    private async Task<ChatCompletionAgent> CreateConversationAgentAsync(FlowTaskStatePlugin flowStatePlugin)
    {
        // Create dedicated kernel for this agent
        var kernel = await _kernelFactory.GetFlowKernelAsync();
        kernel.Plugins.Clear();

        // Add shared execution ID to kernel Data for plugin filters and tracking
        var executionId = this.GetPrimaryKey();
        kernel.Data.Add("System-ExecutionId", executionId.ToString());

        // Add FlowTaskStatePlugin - ConversationAgent needs it to present summaries and check optional fields
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(flowStatePlugin, "FlowTaskStatePlugin"));

        var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            Temperature = 0.7,
            MaxTokens = 500,
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Build template-specific instructions dynamically
        var templateContext = FlowTaskAgentPromptBuilder.BuildTemplateContextForInstructions(_templateDto);
        var instructions = $"""
            You are the ConversationAgent for a Flow Task requirement gathering system.

            ## Template Information
            {templateContext}

            ## Your Responsibilities
            - Engage naturally with the user to collect the information listed above
            - Ask clarifying questions when user responses are unclear or incomplete
            - Reference the specific fields, their descriptions, and why they matter
            - Present summaries when all required information is collected
            - Make the conversation feel natural, friendly, and helpful
            - Be concise but thorough
            - Use markdown formatting and emojis to make messages more engaging and clear
            - Show progress indicators so users know where they are in the process
            - NEVER directly set requirement values - that's RequirementCollectorAgent's job

            ## Markdown & Formatting Guidelines
            - Use **bold** for field names and important terms
            - Use emojis for visual clarity (✅ for success, 📋 for lists, ✏️ for input needed, etc.)
            - Use numbered or bulleted lists for multiple items
            - Use `code formatting` for technical field names when helpful
            - Keep formatting subtle and professional

            ## Workflow Stages

            **Initial Greeting & Overview:**
            - At the start of the conversation, after greeting the user:
              1. Use `FlowTaskStatePlugin.GetPendingRequiredFieldsAsync()` to get the list of all required fields
              2. Present ALL required fields upfront in a numbered list so the user knows what information will be needed
              3. Example format:
                 "I'll help you create this report. I'll need to collect the following **required information**:

                 1. **Plant Name** - The name of the facility
                 2. **Document Title** - Title for the report
                 3. **Projected Start Date** - When the project will begin

                 Let's get started! Can you tell me the plant name?"
              4. This gives users context about the full scope before diving into questions
            - Show progress as you collect fields (e.g., "Great! (1/3 collected)")

            **Normal Collection - Required Fields:**
            - When asking for REQUIRED fields, focus on the specific field needed
            - Mention the field name, its purpose, and why it's important
            - Use field descriptions to explain why the information is needed
            - If validation rules exist for a field, help guide the user to provide appropriate values
            - Acknowledge what the user provides and show progress (e.g., "✅ Got it! (2/3 collected)")
            - Let the RequirementCollectorAgent extract values

            **Normal Collection - Optional Fields:**
            - When asking for OPTIONAL fields (all required fields are already collected):
              1. Use `FlowTaskStatePlugin.GetPendingOptionalFieldsAsync()` to get the list of available optional fields
              2. Present ALL optional fields at once with their descriptions in a clear list format
              3. Let the user choose which optional fields they'd like to provide
              4. Make it clear they can:
                 - Provide values for multiple optional fields in one response
                 - Choose to provide only some optional fields
                 - Choose to skip all optional fields and proceed
              5. Example format:
                 "✅ **All required information collected!**

                 I have a few **optional fields** you can provide if you'd like:

                 • **Project Manager** - Name of the PM
                 • **Budget Code** - Accounting code for the project
                 • **Expected Duration** - Timeline estimate

                 You can provide any, all, or none of these. What would you like to add, or should we proceed with generation?"
            - IMPORTANT: Do NOT ask for optional fields one-by-one. Always present all available optional fields together.

            **Ready for Confirmation (when OrchestratorAgent emits [READY_FOR_CONFIRMATION]):**
            - Use `FlowTaskStatePlugin.GetCollectedValuesAsync()` to retrieve all collected values
            - Use `FlowTaskStatePlugin.GetPendingOptionalFieldsAsync()` to check for uncollected optional fields
            - Present a summary of all collected values in a clear, organized markdown format
            - If there are pending optional fields, list them and mention they can be added
            - Example format:
               "📋 **Summary of Collected Information:**

               • **Plant Name:** Redmond Nuclear Facility
               • **Document Title:** Q4 Environmental Report
               • **Projected Start Date:** 2024-01-15

               Would you like to add any of the optional information, or should we proceed with generating the report?"
            - Make it clear they can either:
              1. Proceed with execution by saying "execute", "proceed", "yes", etc.
              2. Add more optional information by providing it
              3. Make changes to existing values

            ## Available FlowTaskStatePlugin Methods
            - `GetCollectedValuesAsync()` - Get all collected values as JSON (use when presenting summary)
            - `GetPendingRequiredFieldsAsync()` - Get list of required fields not yet collected (use to show full list at start and check progress)
            - `GetPendingOptionalFieldsAsync()` - Get list of optional fields not yet collected (use when presenting optional fields)

            Keep your questions focused on the specific requirements needed for this template.
            """;

        return new ChatCompletionAgent
        {
            Name = "ConversationAgent",
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }

    private async Task<ChatCompletionAgent> CreateRequirementCollectorAgentAsync(FlowTaskStatePlugin flowStatePlugin)
    {
        // Create dedicated kernel for this agent
        var kernel = await _kernelFactory.GetFlowKernelAsync();
        kernel.Plugins.Clear();

        // Add shared execution ID to kernel Data for plugin filters and tracking
        var executionId = this.GetPrimaryKey();
        kernel.Data.Add("System-ExecutionId", executionId.ToString());

        // Add FlowTaskStatePlugin - this agent needs it to store requirement values
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(flowStatePlugin, "FlowTaskStatePlugin"));

        var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            Temperature = 0.3,
            MaxTokens = 300,
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Build field mapping for extraction
        var fieldMappings = FlowTaskAgentPromptBuilder.BuildFieldMappingsForExtraction(_templateDto);
        var instructions = $"""
            You are the RequirementCollectorAgent for a Flow Task system.

            ## Field Mappings
            {fieldMappings}

            ## CRITICAL RULE: Field Name Usage
            - When calling SetRequirementValueAsync, you MUST use the exact "FIELD NAME TO USE" value shown above
            - DO NOT use the "Display Name" - it is for human reference only
            - Example: Use "PlantName" NOT "Plant Name"
            - Example: Use "DocumentTitle" NOT "Document Title"
            - Example: Use "ProjectedProjectStartDate" NOT "Projected Project Start Date"
            - Field names are CASE-SENSITIVE and have NO SPACES unless explicitly shown

            ## Your Responsibilities
            - Analyze user messages to extract values for the fields listed above
            - Check existing state before storing values to avoid duplicates
            - Store extracted values using the FlowTaskStatePlugin with the EXACT field names shown above
            - Match user responses to the appropriate field names based on the descriptions
            - Be precise - only extract values that are clearly stated by the user
            - If a value is ambiguous or unclear, DO NOT store it - the conversation will continue

            ## Available FlowTaskStatePlugin Methods

            **Query Methods (check state first):**
            - `GetCollectedValuesAsync()` - Returns JSON of all currently collected values
              Use this to check what has already been collected before extracting new values
            - `GetRequirementValueAsync(fieldName)` - Get the current value of a specific field
              Use this to check if a field already has a value before overwriting

            **Storage Methods (store extracted values):**
            - `SetRequirementValueAsync(fieldName, value)` - Store a value for a field
              ⚠️ CRITICAL: Use the EXACT "FIELD NAME TO USE" from the mappings above, NOT the Display Name
            - `MarkFieldAsRevisedAsync(fieldName)` - Mark a field as revised by the user
              Use this when the user explicitly changes a previously stored value

            ## Workflow
            1. **Before extraction**: Call GetCollectedValuesAsync() to see what's already been collected
            2. **Extract values**: Analyze the user's message for new information
            3. **Store values**: Use SetRequirementValueAsync(fieldName, value) for each extracted value
               - fieldName MUST be the exact "FIELD NAME TO USE" value (e.g., "PlantName", not "Plant Name")
            4. **Confirm**: Briefly confirm what you stored (e.g., "I've stored PlantName='Redmond Nuclear Facility'")
            5. **Handle revisions**: If the user changes a value, call SetRequirementValueAsync with the new value,
               then call MarkFieldAsRevisedAsync to track the revision

            ## Important Notes
            - Use EXACT field names from the "FIELD NAME TO USE" lines above (case-sensitive, no spaces)
            - Pay attention to field types and validation rules
            - Don't extract the same value twice - check GetCollectedValuesAsync() first
            - Only extract values that are explicitly stated, not implied or assumed
            """;

        return new ChatCompletionAgent
        {
            Name = "RequirementCollectorAgent",
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }

    private async Task<ChatCompletionAgent> CreateValidationAgentAsync(FlowTaskStatePlugin flowStatePlugin)
    {
        // Create dedicated kernel for this agent
        var kernel = await _kernelFactory.GetFlowKernelAsync();
        kernel.Plugins.Clear();

        // Add shared execution ID to kernel Data for plugin filters and tracking
        var executionId = this.GetPrimaryKey();
        kernel.Data.Add("System-ExecutionId", executionId.ToString());

        // Add FlowTaskStatePlugin - this agent needs it to check pending required fields
        kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(flowStatePlugin, "FlowTaskStatePlugin"));

        var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 200,
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions
        };

        // Build required fields reference
        var requiredFieldsReference = FlowTaskAgentPromptBuilder.BuildRequiredFieldsReference(_templateDto);
        var instructions = $"""
            You are the ValidationAgent for a Flow Task system.

            ## Required Fields Reference
            {requiredFieldsReference}

            ## Your Responsibilities
            - Review collected requirements using FlowTaskStatePlugin.GetPendingRequiredFieldsAsync()
            - Check for completeness (all required fields have values)
            - Be concise in your assessment
            - Reference the field descriptions when reporting missing fields

            ## Guidelines
            - If there are pending required fields, list them with their descriptions
            - Example: "We still need ProjectName (name of the project), Location (facility location)"
            - If all required fields are collected, say: "All required information has been collected."
            - Keep your responses brief and factual

            Use the field descriptions above to provide helpful context about what's missing.
            """;

        return new ChatCompletionAgent
        {
            Name = "ValidationAgent",
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }

    private async Task<ChatCompletionAgent> CreateConfirmationAgentAsync()
    {
        // Create dedicated kernel for this agent
        var kernel = await _kernelFactory.GetFlowKernelAsync();
        kernel.Plugins.Clear(); // ConfirmationAgent doesn't need plugins - just interprets user intent

        // Add shared execution ID to kernel Data for plugin filters and tracking
        var executionId = this.GetPrimaryKey();
        kernel.Data.Add("System-ExecutionId", executionId.ToString());

        var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            Temperature = 0.0, // Very low temperature for consistent intent detection
            MaxTokens = 100,
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions
        };

        return new ChatCompletionAgent
        {
            Name = "ConfirmationAgent",
            Instructions = """
                You are the ConfirmationAgent. Your sole responsibility is detecting user intent when they're asked to confirm execution.

                ## Context
                You are invoked when all required information has been collected and the user has been presented with a summary.
                The ConversationAgent asked the user if they want to proceed with execution (e.g., "Would you like to proceed?").

                ## Your Task
                Analyze the user's response and determine their intent using natural language understanding:

                1. **User wants to execute/proceed** - They're confirming they want to proceed
                   Examples: "yes", "sure", "go ahead", "proceed", "generate", "execute", "do it", "let's go", "sounds good"
                   → Emit: [CONFIRM_EXECUTION]

                2. **User wants to provide more information or make changes** - They're not ready to execute
                   Examples: "wait", "actually let me change...", "I need to update...", "can we modify...", "hold on"
                   → Emit: [REQUEST_CHANGES]

                3. **User is asking a question or unclear** - They're uncertain or need clarification
                   Examples: "what will happen?", "can you explain?", "I'm not sure"
                   → Emit: [REQUEST_CLARIFICATION]

                ## Output Format
                Be very brief. Just emit the appropriate tag and optionally a one-sentence interpretation.
                Example: "[CONFIRM_EXECUTION] User is ready to proceed."
                Example: "[REQUEST_CHANGES] User wants to modify the location information."

                Use your natural language understanding. Don't rely on exact keyword matching.
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }

    private async Task<ChatCompletionAgent> CreateOrchestratorAgentAsync()
    {
        // Create dedicated kernel for this agent
        var kernel = await _kernelFactory.GetFlowKernelAsync();
        kernel.Plugins.Clear(); // OrchestratorAgent doesn't need any plugins - just routes

        // Add shared execution ID to kernel Data for plugin filters and tracking
        var executionId = this.GetPrimaryKey();
        kernel.Data.Add("System-ExecutionId", executionId.ToString());

        var executionSettings = new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            MaxTokens = 150,
            ToolCallBehavior = Microsoft.SemanticKernel.Connectors.OpenAI.ToolCallBehavior.AutoInvokeKernelFunctions
        };

        return new ChatCompletionAgent
        {
            Name = "OrchestratorAgent",
            Instructions = """
                You are the OrchestratorAgent managing a Flow Task workflow.

                Your responsibilities:
                - Review agent outputs and route to the appropriate next agent
                - Emit [EXECUTE_OUTPUTS] when ConfirmationAgent signals user wants to execute
                - Emit [COMPLETE] when outputs have been successfully executed
                - Always use [NEXT:AgentName] tags for routing

                ## Workflow Stages

                **Stage 1: Requirement Collection**
                - After ValidationAgent reports missing fields: "[NEXT:ConversationAgent]"
                - Let ConversationAgent handle user interaction during collection

                **Stage 2: Ready for Confirmation (all required fields collected)**
                - When ValidationAgent confirms all required fields are collected
                - Emit "[READY_FOR_CONFIRMATION] [NEXT:ConversationAgent]"
                - ConversationAgent will present summary and ask if user wants to proceed

                **Stage 3: User Confirmation (automatic routing)**
                - After ConversationAgent presents summary, user will respond
                - System automatically routes to ConfirmationAgent (state-driven)
                - ConfirmationAgent will interpret the user's intent and signal back

                **Stage 4: Act on Confirmation**
                - If ConfirmationAgent says [CONFIRM_EXECUTION]: Emit "[EXECUTE_OUTPUTS]" to trigger execution
                - If ConfirmationAgent says [REQUEST_CHANGES]: Route "[NEXT:RequirementCollectorAgent]"
                - If ConfirmationAgent says [REQUEST_CLARIFICATION]: Route "[NEXT:ConversationAgent]"

                **Stage 5: Outputs Executed**
                - When you see [OUTPUTS_EXECUTED] system message: Emit "[COMPLETE]" to terminate the conversation
                - If you see [OUTPUTS_FAILED]: Emit "[COMPLETE]" to terminate (failure state)

                ## Routing Rules
                - ALWAYS include [NEXT:AgentName] to route to the next agent (except when emitting [COMPLETE])
                - Trust ConfirmationAgent's intent detection - don't second-guess it
                - [EXECUTE_OUTPUTS] triggers output execution; [COMPLETE] terminates the conversation
                - Only emit [COMPLETE] when you see [OUTPUTS_EXECUTED] or [OUTPUTS_FAILED]
                """,
            Kernel = kernel,
            Arguments = new KernelArguments(executionSettings)
        };
    }

    private async Task<FlowTaskTemplateDetailDto?> LoadTemplateAsync(Guid templateId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        var template = await db.FlowTaskTemplates
            .Include(t => t.Sections)
                .ThenInclude(s => s.Requirements)
            .Include(t => t.OutputTemplates)
            .Include(t => t.DataSources)
            .FirstOrDefaultAsync(t => t.Id == templateId);

        if (template == null)
        {
            return null;
        }

        return _mapper.Map<FlowTaskTemplateDetailDto>(template);
    }

    private async Task<bool> CheckIfCompleteAsync()
    {
        var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(this.GetPrimaryKey());
        var pendingRequired = await stateGrain.GetPendingRequiredFieldsAsync();
        return string.IsNullOrWhiteSpace(pendingRequired) || pendingRequired == "[]";
    }

    /// <summary>
    /// Generates a text summary output using the collected values.
    /// </summary>
    private async Task<FlowTaskOutput> GenerateTextSummaryAsync(FlowTaskOutputTemplateDto outputTemplate)
    {
        try
        {
            // Get collected values from state grain
            var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(this.GetPrimaryKey());
            var collectedValues = await stateGrain.GetCollectedValuesAsync();

            // Build variables for summary generation
            var variables = new Dictionary<string, object>
            {
                ["template_name"] = _templateDto?.Name ?? string.Empty,
                ["template_description"] = _templateDto?.Description ?? string.Empty,
                ["collected_values"] = collectedValues.Select(kvp => new { key = kvp.Key, value = kvp.Value }).ToList()
            };

            // Render prompt using Scriban
            var promptText = await _promptService.RenderPromptAsync("FlowTaskOutputSummaryPrompt", variables);

            if (string.IsNullOrWhiteSpace(promptText))
            {
                _logger.LogWarning("Failed to render FlowTaskOutputSummaryPrompt");
                return new FlowTaskOutput
                {
                    Name = outputTemplate.Name,
                    Type = "TextSummary",
                    Content = "Summary generation failed",
                    ContentType = "text/plain"
                };
            }

            // Get Flow kernel
            var kernel = await _kernelFactory.GetFlowKernelAsync();
            var chatService = kernel.GetRequiredService<IChatCompletionService>();

            // Create chat history with prompt
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(promptText);

            // Get execution settings
            var executionSettings = await _kernelFactory.GetFlowPromptExecutionSettingsAsync();

            // Get response from LLM
            var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

            return new FlowTaskOutput
            {
                Name = outputTemplate.Name,
                Type = "TextSummary",
                Content = response.Content?.Trim() ?? "Summary generated",
                ContentType = "text/plain"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating text summary");
            return new FlowTaskOutput
            {
                Name = outputTemplate.Name,
                Type = "TextSummary",
                Content = $"Error: {ex.Message}",
                ContentType = "text/plain"
            };
        }
    }

    /// <summary>
    /// Generates a document output by starting document generation process.
    /// </summary>
    private async Task<FlowTaskOutput> GenerateDocumentAsync(FlowTaskOutputTemplateDto outputTemplate)
    {
        try
        {
            // Find the Document Process associated with this Flow Task template
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();

            var documentProcess = await dbContext.DynamicDocumentProcessDefinitions
                .FirstOrDefaultAsync(dp => dp.FlowTaskTemplateId == _state.State.TemplateId);

            if (documentProcess == null)
            {
                _logger.LogError("No Document Process found for FlowTaskTemplateId {TemplateId}", _state.State.TemplateId);
                return new FlowTaskOutput
                {
                    Name = outputTemplate.Name,
                    Type = "Link",
                    Content = "Error: Document Process configuration not found",
                    ContentType = "text/plain"
                };
            }

            // Get collected values from state grain
            var stateGrain = _grainFactory.GetGrain<IFlowTaskStateGrain>(this.GetPrimaryKey());
            var collectedValues = await stateGrain.GetCollectedValuesAsync();

            // Parse user context to extract ProviderSubjectId
            string? providerSubjectId = null;
            if (!string.IsNullOrEmpty(_state.State.UserContext))
            {
                var lines = _state.State.UserContext.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("Provider ID:", StringComparison.OrdinalIgnoreCase))
                    {
                        providerSubjectId = line.Substring("Provider ID:".Length).Trim();
                        break;
                    }
                }
            }

            // Extract DocumentTitle from collected values (required field)
            if (!collectedValues.TryGetValue("DocumentTitle", out var documentTitleObj) ||
                documentTitleObj == null ||
                string.IsNullOrWhiteSpace(documentTitleObj.ToString()))
            {
                _logger.LogError("DocumentTitle not found in collected values for FlowTask {GrainId}", this.GetPrimaryKey());
                return new FlowTaskOutput
                {
                    Name = outputTemplate.Name,
                    Type = "Link",
                    Content = "Error: Document title is required",
                    ContentType = "text/plain"
                };
            }

            var documentTitle = documentTitleObj.ToString();

            // Filter out DocumentTitle from the metadata fields for RequestAsJson
            var metadataFields = collectedValues
                .Where(kvp => kvp.Key != "DocumentTitle")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Create GenerateDocumentDTO with DocumentTitle in property and metadata fields in JSON
            var documentId = Guid.NewGuid();
            var generateDocumentDto = new GenerateDocumentDTO
            {
                Id = documentId,
                DocumentProcessName = documentProcess.ShortName,
                DocumentTitle = documentTitle!,
                ProviderSubjectId = providerSubjectId,
                RequestAsJson = System.Text.Json.JsonSerializer.Serialize(metadataFields)
            };

            // Get the document generation grain and start generation (fire and forget)
            var docGenGrain = _grainFactory.GetGrain<IDocumentGenerationOrchestrationGrain>(documentId);
            _ = docGenGrain.StartDocumentGenerationAsync(generateDocumentDto);

            _logger.LogInformation(
                "Started document generation {DocumentId} for Document Process {ProcessName} from FlowTask {GrainId}",
                documentId,
                documentProcess.ShortName,
                this.GetPrimaryKey());

            // Return a Link output with the document URL
            return new FlowTaskOutput
            {
                Name = outputTemplate.Name,
                Type = "Link",
                Content = $"/docs/{documentId}",
                ContentType = "text/plain",
                Metadata = new Dictionary<string, object>
                {
                    ["documentId"] = documentId.ToString(),
                    ["documentProcessName"] = documentProcess.ShortName,
                    ["status"] = "generating"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating document output");
            return new FlowTaskOutput
            {
                Name = outputTemplate.Name,
                Type = "Link",
                Content = $"Error: {ex.Message}",
                ContentType = "text/plain"
            };
        }
    }

    /// <summary>
    /// Formats output results into a user-friendly message with markdown formatting.
    /// </summary>
    /// <param name="outputResult">The output result to format.</param>
    /// <returns>A formatted message string with markdown and emojis.</returns>
    private string FormatOutputsForUser(FlowTaskOutputResult outputResult)
    {
        if (!outputResult.IsSuccess || outputResult.Outputs.Count == 0)
        {
            return "❌ I've completed collecting information, but encountered an issue generating the outputs. Please check the logs.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("✅ **Processing Complete!**");
        sb.AppendLine();
        sb.AppendLine("I've finished collecting your requirements and generating the outputs. Here are your results:");
        sb.AppendLine();

        foreach (var output in outputResult.Outputs)
        {
            switch (output.Type)
            {
                case "TextSummary":
                    sb.AppendLine($"📄 **{output.Name}**");
                    sb.AppendLine(output.Content);
                    sb.AppendLine();
                    break;

                case "Link":
                    sb.AppendLine($"📎 **{output.Name}**");
                    // Format as clickable markdown link
                    var linkUrl = output.Content?.Trim() ?? string.Empty;
                    if (linkUrl.StartsWith("/"))
                    {
                        sb.AppendLine($"Your document is being generated: [View Document]({linkUrl})");
                    }
                    else
                    {
                        sb.AppendLine($"[{output.Name}]({linkUrl})");
                    }
                    sb.AppendLine();
                    break;

                default:
                    sb.AppendLine($"📋 **{output.Name}** ({output.Type})");
                    sb.AppendLine(output.Content);
                    sb.AppendLine();
                    break;
            }
        }

        return sb.ToString();
    }

}
