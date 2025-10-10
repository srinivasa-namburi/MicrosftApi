// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Grains.Chat.Contracts.Models;
using Microsoft.Greenlight.Grains.Chat.Contracts.State;
using Microsoft.Greenlight.Grains.Chat.Services;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Chat;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.Greenlight.Shared.Contracts.Messages.Chat.Events;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using Microsoft.Greenlight.Shared.Prompts;
using Microsoft.Greenlight.Shared.Services;
using Microsoft.Greenlight.Shared.Services.Search;
using Microsoft.Greenlight.Shared.Services.Search.Abstractions;
using Microsoft.Greenlight.Shared.Contracts;
using Orleans.Concurrency;
using Orleans.Streams;
using System.Text;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Greenlight.Grains.Chat;

/// <summary>
/// Flow Orchestration Grain - orchestrates multiple backend document process conversations and emits a synthesized answer.
/// Streaming aggregation updates are pushed to the UI through the normal SignalR notification pipeline so
/// the UI does not need Flow-specific transport logic.
/// </summary>
[Reentrant]
public class FlowOrchestrationGrain : Grain<FlowOrchestrationState>, IFlowOrchestrationGrain
{
    private readonly IPersistentState<FlowOrchestrationState> _state;
    private readonly ILogger<FlowOrchestrationGrain> _logger;
    private readonly IDocumentProcessInfoService _documentProcessInfoService;
    private readonly IFlowTaskTemplateService _flowTaskTemplateService;
    private readonly IFlowTaskTemplateResolver _flowTaskTemplateResolver;
    private readonly ISystemPromptInfoService _systemPromptInfoService;
    private readonly IDocumentRepositoryFactory _documentRepositoryFactory;
    private readonly Dictionary<string, CancellationTokenSource> _activeProcessingTasks = new();
    private string? _currentProcessingTaskId;
    private FlowSessionStatus _currentStatus = FlowSessionStatus.Created;
    private string? _currentResponse;
    private ConcurrencyLease? _concurrencyLease;

    // Backend conversation reuse
    private readonly Dictionary<string, Guid> _processToBackendConversation = new(StringComparer.OrdinalIgnoreCase);

    // Per-message state tracking for concurrent message handling
    private readonly ConcurrentDictionary<Guid, MessageAggregationState> _messageStates = new();
    private readonly ConcurrentDictionary<Guid, Guid> _backendToUserMessageMap = new();

    // MCP-specific request tracking
    // Track active MCP requests to differentiate from UI requests
    private readonly HashSet<Guid> _activeMcpRequests = new();
    // Track backend completion status for MCP requests
    private readonly Dictionary<Guid, McpRequestState> _mcpRequestStates = new();
    // Map backend conversation IDs to MCP request IDs
    private readonly Dictionary<Guid, Guid> _backendToMcpRequestMap = new();

    private readonly IKernelFactory _kernelFactory;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Gets the current Flow configuration options, refreshed from IConfiguration on each access.
    /// </summary>
    private ServiceConfigurationOptions.FlowOptions FlowOptions
    {
        get
        {
            var flowOptions = _configuration
                .GetSection("ServiceConfiguration:GreenlightServices:Flow")
                .Get<ServiceConfigurationOptions.FlowOptions>()
                ?? new ServiceConfigurationOptions.FlowOptions();

            return flowOptions;
        }
    }

    private static readonly TimeSpan AggregationPushMinInterval = TimeSpan.FromMilliseconds(400);
    private const int AggregationMinLengthDelta = 120;
    private Timer? _cleanupTimer;

    private enum FlowTaskIntentType
    {
        None,
        UseActiveFlowTask,
        StartNewFlowTask
    }

    private sealed record FlowTaskIntentDecision(FlowTaskIntentType IntentType, FlowTaskTemplateInfo? Template = null);

    private sealed record FlowTaskMessageResult(
        bool Success,
        string? ResponseMessage,
        FlowTaskExecutionState State,
        bool FlowTaskCompleted,
        string? CompletionMessage,
        string? ErrorMessage = null);

    public FlowOrchestrationGrain(
        [PersistentState("flowOrchestration")] IPersistentState<FlowOrchestrationState> state,
        ILogger<FlowOrchestrationGrain> logger,
        IDocumentProcessInfoService documentProcessInfoService,
        IFlowTaskTemplateService flowTaskTemplateService,
        IFlowTaskTemplateResolver flowTaskTemplateResolver,
        ISystemPromptInfoService systemPromptInfoService,
        IDocumentRepositoryFactory documentRepositoryFactory,
        IKernelFactory kernelFactory,
        IConfiguration configuration)
    {
        _state = state;
        _logger = logger;
        _flowTaskTemplateService = flowTaskTemplateService;
        _flowTaskTemplateResolver = flowTaskTemplateResolver;
        _documentProcessInfoService = documentProcessInfoService;
        _systemPromptInfoService = systemPromptInfoService;
        _documentRepositoryFactory = documentRepositoryFactory;
        _kernelFactory = kernelFactory;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.SessionId == Guid.Empty)
        {
            _state.State = _state.State with
            {
                SessionId = this.GetPrimaryKey(),
                CreatedUtc = DateTime.UtcNow,
                LastActivityUtc = DateTime.UtcNow
            };
            await _state.WriteStateAsync();
        }
        try
        {
            var streamProvider = this.GetStreamProvider("StreamProvider");

            // Subscribe to backend conversation updates
            var stream = streamProvider.GetStream<FlowBackendConversationUpdate>("FlowBackendConversationUpdate", _state.State.SessionId);
            await stream.SubscribeAsync(OnFlowBackendConversationUpdate);
            _logger.LogInformation("Subscribed to Flow backend conversation updates for session {SessionId}", _state.State.SessionId);

            // Subscribe to backend status updates
            var statusStream = streamProvider.GetStream<FlowBackendStatusUpdate>("FlowBackendStatusUpdate", _state.State.SessionId);
            await statusStream.SubscribeAsync(OnFlowBackendStatusUpdate);
            _logger.LogInformation("Subscribed to Flow backend status updates for session {SessionId}", _state.State.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed subscribing Flow session {SessionId} to backend updates", _state.State.SessionId);
        }

        // Set up periodic cleanup timer
        _cleanupTimer = new Timer(
            callback: _ => CleanupOldMessageStates(),
            state: null,
            dueTime: TimeSpan.FromMinutes(5),
            period: TimeSpan.FromMinutes(5));

        _logger.LogInformation("Flow orchestration grain activated {SessionId}", _state.State.SessionId);
        await base.OnActivateAsync(cancellationToken);
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        _cleanupTimer?.Dispose();
        _logger.LogInformation("Flow orchestration grain deactivated {SessionId}", _state.State.SessionId);
        return base.OnDeactivateAsync(reason, cancellationToken);
    }

    #region Public Flow API
    public async Task<FlowQueryResult> ProcessDocumentQueryAsync(string message, string context)
    {
        var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(ConcurrencyCategory.FlowChat.ToString());
        var requesterId = $"Flow:{_state.State.SessionId}";
        try
        {
            _concurrencyLease = await coordinator.AcquireAsync(requesterId, 1, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(30));
            _state.State = _state.State with { LastActivityUtc = DateTime.UtcNow };

            // When ProcessDocumentQueryAsync is called directly (not through ProcessMessageAsync),
            // we need to create the user message. But when called from ProcessMessageAsync,
            // the message is already stored.
            Guid userMessageId;
            MessageAggregationState messageState;

            // Check if we already have a recent user message with this content
            var recentMessage = _state.State.UserConversationMessages
                .Where(m => m.Source == ChatMessageSource.User)
                .OrderByDescending(m => m.CreatedUtc)
                .FirstOrDefault(m => m.Message == message && (DateTime.UtcNow - m.CreatedUtc).TotalSeconds < 5);

            if (recentMessage != null)
            {
                // Message already exists (came from ProcessMessageAsync)
                userMessageId = recentMessage.Id;
                messageState = _messageStates.GetOrAdd(userMessageId, id => new MessageAggregationState
                {
                    MessageId = id,
                    LastAggregationPushUtc = DateTime.MinValue,
                    LastAggregationLength = 0
                });
                _logger.LogDebug("Using existing user message {MessageId} from ProcessMessageAsync", userMessageId);
            }
            else
            {
                // Direct call to ProcessDocumentQueryAsync - need to create the message
                userMessageId = Guid.NewGuid();
                var userMessage = new ChatMessage
                {
                    Id = userMessageId,
                    ConversationId = _state.State.SessionId,
                    Message = message,
                    Source = ChatMessageSource.User,
                    CreatedUtc = DateTime.UtcNow,
                    UserId = _state.State.ProviderSubjectId // Store the user's provider ID
                };

                // Create UserInformation for the user message
                if (!string.IsNullOrEmpty(_state.State.ProviderSubjectId))
                {
                    userMessage.AuthorUserInformation = new UserInformation
                    {
                        Id = Guid.NewGuid(), // Generate a new ID for the UserInformation entity
                        ProviderSubjectId = _state.State.ProviderSubjectId,
                        FullName = _state.State.UserName ?? "Unknown",
                        Provider = AuthenticationProvider.AzureAD
                    };
                    userMessage.AuthorUserInformationId = userMessage.AuthorUserInformation.Id;
                }

                messageState = new MessageAggregationState
                {
                    MessageId = userMessageId,
                    LastAggregationPushUtc = DateTime.MinValue,
                    LastAggregationLength = 0
                };
                _messageStates[userMessageId] = messageState;

                // Add to persisted state
                var messages = _state.State.UserConversationMessages.ToList();
                messages.Add(userMessage);
                _state.State = _state.State with { UserConversationMessages = messages, QueryCount = _state.State.QueryCount + 1 };
                await _state.WriteStateAsync();
                _logger.LogDebug("Created new user message {MessageId} in ProcessDocumentQueryAsync", userMessageId);
            }

            var processesToUse = await ResolveDocumentProcessesForMessageAsync(message, context);

            string response;
            if (processesToUse.Any())
            {
                // We have specific processes to engage - delegate to backend conversations
                _logger.LogInformation("Processing message with {ProcessCount} backend processes", processesToUse.Count);

                // Fire and forget - responses will come via streams
                await ProcessConversationsAsync(message, processesToUse, userMessageId);

                // The synthesis will happen via stream updates and aggregation sections
                response = "Processing your request across multiple document processes. Results will be aggregated as they arrive.";
            }
            else
            {
                // No specific processes - respond conversationally using Flow's own capabilities (if enabled)
                if (FlowOptions.EnableConversationalFallback)
                {
                    _logger.LogInformation("No specific processes engaged, responding conversationally");
                    response = await GenerateDirectFlowResponseAsync(message);

                    // Create and send a direct Flow response via SignalR to the conversation ID
                    await SendDirectFlowResponseAsync(response);
                }
                else
                {
                    _logger.LogInformation("No specific processes engaged and conversational fallback is disabled");
                    response = "I don't have enough context to assist with that request. Please try being more specific about what you'd like me to help with.";
                    await SendDirectFlowResponseAsync(response);
                }
            }

            await _state.WriteStateAsync();
            return new FlowQueryResult { Response = response, ConversationIds = _state.State.ActiveBackendConversationIds, Status = "completed" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flow query failure {SessionId}", _state.State.SessionId);
            return new FlowQueryResult { Response = "I encountered an error while processing your request.", ConversationIds = _state.State.ActiveBackendConversationIds, Status = "error", Error = ex.Message };
        }
        finally
        {
            if (_concurrencyLease != null)
            {
                try { await coordinator.ReleaseAsync(_concurrencyLease.LeaseId); } catch (Exception ex) { _logger.LogWarning(ex, "Release lease failed"); }
                _concurrencyLease = null;
            }
        }
    }

    public async Task<string> StartStreamingMessageProcessingAsync(string message, string context)
    {
        var taskId = Guid.NewGuid().ToString("N")[..12];
        var cts = new CancellationTokenSource();
        _activeProcessingTasks[taskId] = cts;
        _currentProcessingTaskId = taskId;
        _currentStatus = FlowSessionStatus.Processing;
        _ = ProcessStreamingMessageInBackgroundAsync(taskId, message, context, cts.Token);
        return taskId;
    }

    public async Task CancelProcessingAsync()
    {
        foreach (var kv in _activeProcessingTasks.ToList())
        {
            try { kv.Value.Cancel(); } catch { }
        }
        _activeProcessingTasks.Clear();
        _currentProcessingTaskId = null;
        if (_currentStatus == FlowSessionStatus.Processing) { _currentStatus = FlowSessionStatus.Cancelled; }
        _logger.LogInformation("Flow processing cancelled {SessionId}", _state.State.SessionId);
    }

    #region MCP-Specific Processing
    /// <summary>
    /// Process a query for MCP clients - waits for all backends to complete.
    /// This method does NOT emit SignalR updates, only returns the final response.
    /// </summary>
    public async Task<FlowQueryResult> ProcessMessageForMcpAsync(
        string message,
        string context,
        int timeoutSeconds = 60)
    {
        var mcpRequestId = Guid.NewGuid();
        _logger.LogInformation("Starting MCP query processing {RequestId} for session {SessionId}",
            mcpRequestId, _state.State.SessionId);

        _activeMcpRequests.Add(mcpRequestId);

        try
        {
            var mcpState = new McpRequestState
            {
                RequestId = mcpRequestId,
                StartedUtc = DateTime.UtcNow
            };
            _mcpRequestStates[mcpRequestId] = mcpState;

            var userMessageId = Guid.NewGuid();
            mcpState.UserMessageId = userMessageId;

            var userMessage = new ChatMessage
            {
                Id = userMessageId,
                ConversationId = _state.State.SessionId,
                Message = message,
                Source = ChatMessageSource.User,
                CreatedUtc = DateTime.UtcNow,
                UserId = _state.State.ProviderSubjectId
            };

            _state.State = _state.State with
            {
                UserConversationMessages = _state.State.UserConversationMessages.Append(userMessage).ToList(),
                QueryCount = _state.State.QueryCount + 1,
                LastActivityUtc = DateTime.UtcNow
            };
            await _state.WriteStateAsync();

            _currentStatus = FlowSessionStatus.Processing;
            _currentResponse = null;

            var chatMessageDto = new ChatMessageDTO
            {
                Id = userMessageId,
                ConversationId = _state.State.SessionId,
                Message = message,
                Source = ChatMessageSource.User,
                CreatedUtc = userMessage.CreatedUtc,
                UserId = userMessage.UserId,
                UserFullName = _state.State.UserName ?? "User"
            };

            var flowTaskDecision = await DetermineFlowTaskIntentAsync(message);
            switch (flowTaskDecision.IntentType)
            {
                case FlowTaskIntentType.UseActiveFlowTask:
                {
                    var taskResult = await RouteMessageToFlowTaskAsync(chatMessageDto, notifyClients: false);
                    _currentStatus = taskResult.FlowTaskCompleted ? FlowSessionStatus.Completed : FlowSessionStatus.Processing;
                    _currentResponse = taskResult.ResponseMessage ?? taskResult.CompletionMessage;
                    CleanupMcpRequestState(mcpRequestId);
                    return CreateFlowTaskQueryResult(taskResult);
                }
                case FlowTaskIntentType.StartNewFlowTask when flowTaskDecision.Template != null:
                {
                    var taskResult = await StartFlowTaskExecutionAsync(chatMessageDto, flowTaskDecision.Template, notifyClients: false);
                    _currentStatus = taskResult.FlowTaskCompleted ? FlowSessionStatus.Completed : FlowSessionStatus.Processing;
                    _currentResponse = taskResult.ResponseMessage ?? taskResult.CompletionMessage;
                    CleanupMcpRequestState(mcpRequestId);
                    return CreateFlowTaskQueryResult(taskResult);
                }
            }

            var documentProcesses = await ResolveDocumentProcessesForMessageAsync(message, context);

            if (documentProcesses.Any())
            {
                _logger.LogInformation("MCP: Engaging {ProcessCount} document processes: {Processes}",
                    documentProcesses.Count, string.Join(", ", documentProcesses));

                await SendMcpQueriesToBackendsAsync(message, documentProcesses, mcpRequestId, userMessageId);

                _logger.LogInformation("MCP: Waiting for all backends to complete (timeout: {TimeoutSeconds}s)", timeoutSeconds);
                await WaitForMcpBackendsToCompleteAsync(mcpRequestId, TimeSpan.FromSeconds(timeoutSeconds));

                _logger.LogInformation("MCP: All backends complete, synthesizing final response");
                var finalResponse = await SynthesizeMcpResponseAsync(mcpState);

                var assistantMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = _state.State.SessionId,
                    Message = finalResponse,
                    Source = ChatMessageSource.Assistant,
                    CreatedUtc = DateTime.UtcNow,
                    ReplyToChatMessageId = userMessageId
                };

                _state.State = _state.State with
                {
                    UserConversationMessages = _state.State.UserConversationMessages.Append(assistantMessage).ToList(),
                    LastActivityUtc = DateTime.UtcNow
                };
                await _state.WriteStateAsync();

                _currentStatus = FlowSessionStatus.Completed;
                _currentResponse = finalResponse;

                return new FlowQueryResult
                {
                    Response = finalResponse,
                    ConversationIds = mcpState.PendingBackendConversations.ToList(),
                    Status = "completed"
                };
            }

            _logger.LogInformation("MCP: No document processes engaged, generating direct response");
            var directResponse = await GenerateDirectFlowResponseAsync(message);

            var directMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = _state.State.SessionId,
                Message = directResponse,
                Source = ChatMessageSource.Assistant,
                CreatedUtc = DateTime.UtcNow,
                ReplyToChatMessageId = userMessageId
            };

            _state.State = _state.State with
            {
                UserConversationMessages = _state.State.UserConversationMessages.Append(directMessage).ToList(),
                LastActivityUtc = DateTime.UtcNow
            };
            await _state.WriteStateAsync();

            _currentStatus = FlowSessionStatus.Completed;
            _currentResponse = directResponse;

            return new FlowQueryResult
            {
                Response = directResponse,
                ConversationIds = new List<Guid>(),
                Status = "completed"
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("MCP query {RequestId} cancelled/timed out", mcpRequestId);
            _currentStatus = FlowSessionStatus.Cancelled;
            return new FlowQueryResult
            {
                Response = "The request timed out while waiting for backend processes to complete.",
                Status = "timeout",
                Error = "Operation cancelled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP query {RequestId} failed", mcpRequestId);
            _currentStatus = FlowSessionStatus.Error;
            _currentResponse = ex.Message;
            return new FlowQueryResult
            {
                Response = "An error occurred while processing your request.",
                Status = "error",
                Error = ex.Message
            };
        }
        finally
        {
            CleanupMcpRequestState(mcpRequestId);
            _logger.LogInformation("MCP query {RequestId} completed and cleaned up", mcpRequestId);
        }
    }

    private void CleanupMcpRequestState(Guid mcpRequestId)
    {
        _activeMcpRequests.Remove(mcpRequestId);
        _mcpRequestStates.Remove(mcpRequestId);

        var backendIds = _backendToMcpRequestMap
            .Where(kvp => kvp.Value == mcpRequestId)
            .Select(kvp => kvp.Key)
            .ToList();
        foreach (var backendId in backendIds)
        {
            _backendToMcpRequestMap.Remove(backendId);
        }
    }

    /// <summary>
    /// Send queries to backend conversations for MCP request.
    /// Tracks which backends are associated with this MCP request.
    /// </summary>
    private async Task SendMcpQueriesToBackendsAsync(
        string message,
        List<string> documentProcesses,
        Guid mcpRequestId,
        Guid userMessageId)
    {
        var mcpState = _mcpRequestStates[mcpRequestId];

        foreach (var processName in documentProcesses)
        {
            try
            {
                // Get or create conversation for this process
                var conversationId = await GetOrCreateConversationForProcessAsync(processName);

                // Track this backend for the MCP request
                mcpState.PendingBackendConversations.Add(conversationId);
                _backendToMcpRequestMap[conversationId] = mcpRequestId;

                // Send the message to the backend
                var conversationGrain = GrainFactory.GetGrain<IConversationGrain>(conversationId);
                var backendMessageId = Guid.NewGuid();

                var chatMessageDto = new ChatMessageDTO
                {
                    Id = backendMessageId,
                    ConversationId = conversationId,
                    Message = message,
                    Source = ChatMessageSource.User,
                    CreatedUtc = DateTime.UtcNow,
                    UserId = _state.State.ProviderSubjectId,
                    UserFullName = _state.State.UserName ?? "User"
                };

                _logger.LogInformation("MCP: Sending message to backend {ProcessName} (conversation {ConversationId})",
                    processName, conversationId);

                // Fire and forget - responses will come via streams
                await conversationGrain.ProcessMessageAsync(chatMessageDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP: Failed to send message to backend {ProcessName}", processName);
                // Remove from pending if send failed
                mcpState.PendingBackendConversations.Remove(
                    mcpState.PendingBackendConversations.FirstOrDefault());
            }
        }
    }

    /// <summary>
    /// Wait for all backend conversations to complete for an MCP request.
    /// Uses polling to check completion status with timeout protection.
    /// </summary>
    private async Task WaitForMcpBackendsToCompleteAsync(
        Guid mcpRequestId,
        TimeSpan timeout)
    {
        var mcpState = _mcpRequestStates[mcpRequestId];
        var startTime = DateTime.UtcNow;

        while (true)
        {
            // Check if all backends are complete
            if (mcpState.AllBackendsComplete || !mcpState.PendingBackendConversations.Any())
            {
                _logger.LogInformation("MCP: All backends complete for request {RequestId}", mcpRequestId);
                return;
            }

            // Check for timeout
            if (DateTime.UtcNow - startTime > timeout)
            {
                _logger.LogWarning("MCP: Timeout waiting for backends after {TimeoutSeconds}s, request {RequestId}",
                    timeout.TotalSeconds, mcpRequestId);
                throw new OperationCanceledException($"Timeout waiting for backend responses after {timeout.TotalSeconds} seconds");
            }

            // Orleans-safe delay before next check
            await Task.Delay(100);
        }
    }

    /// <summary>
    /// Synthesize the final response from all backend responses for MCP.
    /// </summary>
    private async Task<string> SynthesizeMcpResponseAsync(McpRequestState mcpState)
    {
        if (!mcpState.BackendResponses.Any())
        {
            return "I wasn't able to retrieve information from the document processes.";
        }

        if (mcpState.BackendResponses.Count == 1)
        {
            return mcpState.BackendResponses.Values.First();
        }

        // Multiple responses - synthesize them
        var sb = new StringBuilder();
        sb.AppendLine("Based on the available information:");

        foreach (var kvp in mcpState.BackendResponses.OrderBy(k => k.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"**From {kvp.Key}:**");
            sb.AppendLine(kvp.Value);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Handle backend updates for MCP requests - track completion without SignalR.
    /// </summary>
    private async Task HandleMcpBackendUpdate(Guid mcpRequestId, FlowBackendConversationUpdate update)
    {
        if (!_mcpRequestStates.TryGetValue(mcpRequestId, out var mcpState))
        {
            _logger.LogWarning("MCP state not found for request {RequestId}", mcpRequestId);
            return;
        }

        // Only process assistant messages (final responses)
        if (update.ChatMessageDto.Source == ChatMessageSource.Assistant && update.IsComplete)
        {
            var processName = update.DocumentProcessName ?? "unknown";
            var responseContent = update.ChatMessageDto.Message ?? string.Empty;

            _logger.LogInformation("MCP: Received complete response from {ProcessName} for request {RequestId}",
                processName, mcpRequestId);

            // Store the backend response
            mcpState.BackendResponses[processName] = responseContent;

            // Remove this backend from pending list
            mcpState.PendingBackendConversations.Remove(update.BackendConversationId);

            // Check if all backends are complete
            if (!mcpState.PendingBackendConversations.Any())
            {
                _logger.LogInformation("MCP: All backends complete for request {RequestId}", mcpRequestId);
                mcpState.AllBackendsComplete = true;
            }
        }
    }
    #endregion

    public Task<FlowSessionState> GetStateAsync() => Task.FromResult(new FlowSessionState
    {
        SessionId = _state.State.SessionId,
        CreatedUtc = _state.State.CreatedUtc,
        LastActivityUtc = _state.State.LastActivityUtc,
        UserOid = _state.State.ProviderSubjectId,
        UserName = _state.State.UserName,
        ActiveConversationIds = _state.State.ActiveBackendConversationIds,
        EngagedDocumentProcesses = _state.State.EngagedDocumentProcesses,
        AvailablePlugins = _state.State.AvailablePlugins.ToList(),
        ActiveCapabilities = _state.State.ActiveCapabilities,
        QueryCount = _state.State.QueryCount,
        Status = _currentStatus,
        CurrentResponse = _currentResponse
    });

    public async Task InitializeAsync(string providerSubjectId, string? userName = null)
    {
        var defaultPrompt = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowUserConversationSystemPrompt);
        _state.State = _state.State with
        {
            ProviderSubjectId = providerSubjectId,
            UserName = userName,
            SystemPrompt = string.IsNullOrWhiteSpace(_state.State.SystemPrompt) ? defaultPrompt : _state.State.SystemPrompt,
            LastActivityUtc = DateTime.UtcNow
        };
        await _state.WriteStateAsync();

        // Update any existing backend conversations with the user context
        // This ensures MCP tool authentication works even if conversations were created before user context was set
        if (!string.IsNullOrWhiteSpace(providerSubjectId) && _state.State.ActiveBackendConversationIds.Any())
        {
            foreach (var conversationId in _state.State.ActiveBackendConversationIds)
            {
                try
                {
                    var conv = GrainFactory.GetGrain<IConversationGrain>(conversationId);
                    await conv.SetUserContextAsync(providerSubjectId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set user context on existing backend conversation {ConversationId}", conversationId);
                }
            }
        }
    }
    #endregion

    #region Conversation-orchestrated message interface
    private async Task<FlowTaskIntentDecision> DetermineFlowTaskIntentAsync(string message)
    {
        if (_state.State.ActiveFlowTaskExecutionId.HasValue)
        {
            _logger.LogInformation("Active Flow Task execution detected {ExecutionId}, routing message to existing execution",
                _state.State.ActiveFlowTaskExecutionId.Value);
            return new FlowTaskIntentDecision(FlowTaskIntentType.UseActiveFlowTask);
        }

        var template = await _flowTaskTemplateResolver.DetermineFlowTaskTemplateAsync(message, FlowOptions);
        if (template != null)
        {
            _logger.LogInformation("Detected Flow Task intent for template '{TemplateName}' ({TemplateId})",
                template.Name, template.Id);
            return new FlowTaskIntentDecision(FlowTaskIntentType.StartNewFlowTask, template);
        }

        return new FlowTaskIntentDecision(FlowTaskIntentType.None);
    }

    public async Task ProcessMessageAsync(ChatMessageDTO chatMessageDto)
    {
        _logger.LogInformation("Flow processing message {MessageId} for conversation {ConversationId}, Flow session {SessionId}",
            chatMessageDto.Id, chatMessageDto.ConversationId, _state.State.SessionId);

        // Create per-message state for this new message
        var messageState = new MessageAggregationState
        {
            MessageId = chatMessageDto.Id,
            LastAggregationPushUtc = DateTime.MinValue,
            LastAggregationLength = 0
        };
        _messageStates[chatMessageDto.Id] = messageState;

        var userMessage = new ChatMessage
        {
            Id = chatMessageDto.Id,
            ConversationId = chatMessageDto.ConversationId,
            Source = chatMessageDto.Source, // Preserve the actual source from the DTO
            Message = chatMessageDto.Message,
            ContentText = chatMessageDto.ContentText,
            CreatedUtc = chatMessageDto.CreatedUtc,
            ModifiedUtc = DateTime.UtcNow,
            UserId = chatMessageDto.UserId // Store the actual string user ID from the provider
        };

        // Create UserInformation if this is a user message with UserId
        if (chatMessageDto.Source == ChatMessageSource.User && !string.IsNullOrEmpty(chatMessageDto.UserId))
        {
            userMessage.AuthorUserInformation = new UserInformation
            {
                Id = Guid.NewGuid(), // Generate a new ID for the UserInformation entity
                ProviderSubjectId = chatMessageDto.UserId,
                FullName = chatMessageDto.UserFullName ?? "Unknown",
                Provider = AuthenticationProvider.AzureAD
            };
            userMessage.AuthorUserInformationId = userMessage.AuthorUserInformation.Id;
        }
        _state.State = _state.State with
        {
            UserConversationMessages = _state.State.UserConversationMessages.Append(userMessage).ToList(),
            LastActivityUtc = DateTime.UtcNow,
            QueryCount = _state.State.QueryCount + 1
        };
        await _state.WriteStateAsync();
        await ProcessFlowMessageAsync(chatMessageDto);
    }

    private async Task ProcessFlowMessageAsync(ChatMessageDTO chatMessageDto)
    {
        try
        {
            // Send initial "Responding..." status to show Flow is processing
            try
            {
                var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                var initialStatus = new ChatMessageStatusNotification(
                    chatMessageDto.Id,
                    "Responding...")
                {
                    ProcessingComplete = false,
                    Persistent = false
                };
                await notifier.NotifyChatMessageStatusAsync(initialStatus);
            }
            catch (Exception statusEx)
            {
                _logger.LogDebug(statusEx, "Failed to send initial responding status");
            }

            var flowTaskDecision = await DetermineFlowTaskIntentAsync(chatMessageDto.Message ?? string.Empty);
            switch (flowTaskDecision.IntentType)
            {
                case FlowTaskIntentType.UseActiveFlowTask:
                    await RouteMessageToFlowTaskAsync(chatMessageDto, notifyClients: true);
                    return;
                case FlowTaskIntentType.StartNewFlowTask:
                    if (flowTaskDecision.Template != null)
                    {
                        await StartFlowTaskExecutionAsync(chatMessageDto, flowTaskDecision.Template, notifyClients: true);
                        return;
                    }
                    break;
            }

            // No Flow Task detected - proceed with document process orchestration
            _logger.LogInformation("Processing Flow query: {Message}", chatMessageDto.Message);
            var result = await ProcessDocumentQueryAsync(chatMessageDto.Message ?? string.Empty, string.Empty); // streaming updates will appear
            _logger.LogInformation("Flow query completed with status: {Status}", result.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flow message processing error for message: {Message}", chatMessageDto.Message);
        }
    }

    public Task<List<ChatMessageDTO>> GetMessagesAsync()
    {
        // Return all messages for loading existing conversations
        // BUT exclude superseded messages (intermediate aggregations)
        // The UI needs user messages when loading an existing conversation
        return Task.FromResult(_state.State.UserConversationMessages
            .Where(m => !_state.State.SupersededMessages.ContainsKey(m.Id)) // Exclude superseded messages
            .OrderBy(m => m.CreatedUtc)
            .Select(m =>
            {
                var dto = MapToDto(m);
                // Include superseding info if this message supersedes another
                if (_state.State.SupersededMessages.TryGetValue(m.Id, out var supersededBy))
                {
                    dto.SupersededByMessageId = supersededBy;
                }
                // Mark intermediate aggregation messages properly
                if (m.Source == ChatMessageSource.Assistant && m.Message != null &&
                    (m.Message.Contains("Assembling responses from document process backends") ||
                     m.Message.Contains("backend conversations complete")))
                {
                    dto.IsFlowAggregation = true;
                    dto.IsIntermediate = true;
                }
                return dto;
            })
            .ToList());
    }
    #endregion

    #region Backend Conversation Processing
    private async Task<List<string>> ResolveDocumentProcessesForMessageAsync(string message, string context)
    {
        var newIntentProcesses = new List<string>();
        if (FlowOptions.EnableIntentDetection)
        {
            _logger.LogInformation("Checking for document process intent in message: {Message}", message);
            newIntentProcesses = await DetermineDocumentProcessesAsync(message, context);
            _logger.LogInformation("Found {ProcessCount} new intent processes: {Processes}", newIntentProcesses.Count,
                string.Join(", ", newIntentProcesses));
        }
        else
        {
            _logger.LogInformation("Intent detection is disabled via configuration");
        }

        if (newIntentProcesses.Any())
        {
            _logger.LogInformation("New intent discovered, updating engaged processes");
            var updatedEngaged = _state.State.EngagedDocumentProcesses.ToList();
            foreach (var process in newIntentProcesses)
            {
                if (!updatedEngaged.Contains(process, StringComparer.OrdinalIgnoreCase))
                {
                    updatedEngaged.Add(process);
                    _logger.LogInformation("Engaging new document process: {ProcessName}", process);
                }
            }

            _state.State = _state.State with { EngagedDocumentProcesses = updatedEngaged };
            return newIntentProcesses;
        }

        var processesToUse = _state.State.EngagedDocumentProcesses.ToList();
        _logger.LogInformation("No new intent detected, using {ProcessCount} existing engaged processes: {Processes}",
            processesToUse.Count, string.Join(", ", processesToUse));
        return processesToUse;
    }

    private async Task ProcessConversationsAsync(string message, List<string> documentProcesses, Guid userMessageId)
    {
        // Fire and forget - send messages to all backend conversations without waiting
        var tasks = documentProcesses.Select(p => SendMessageToBackendAsync(message, p, userMessageId)).ToList();
        await Task.WhenAll(tasks);

        // The responses will come back via streams and update the aggregation sections
        _logger.LogInformation("Sent messages to {ProcessCount} backend conversations, waiting for stream updates", documentProcesses.Count);
    }

    private async Task SendMessageToBackendAsync(string message, string processName, Guid userMessageId)
    {
        try
        {
            _logger.LogInformation("Sending message to backend for document process: {ProcessName}", processName);
            var conversationId = await GetOrCreateConversationForProcessAsync(processName);
            _logger.LogInformation("Using backend conversation {ConversationId} for process {ProcessName}", conversationId, processName);

            var conversationGrain = GrainFactory.GetGrain<IConversationGrain>(conversationId);
            var backendMessageId = Guid.NewGuid();
            var chatMessageDto = new ChatMessageDTO
            {
                Id = backendMessageId,
                ConversationId = conversationId,
                Message = message,
                Source = ChatMessageSource.User,
                CreatedUtc = DateTime.UtcNow,
                UserId = _state.State.ProviderSubjectId,
                UserFullName = _state.State.UserName ?? "User"
            };

            // Track correlation between backend CONVERSATION and user message
            // This is crucial - we correlate by conversation ID, not message ID
            _backendToUserMessageMap[conversationId] = userMessageId;
            _logger.LogInformation("Correlating backend conversation {ConversationId} to user message {UserMessageId}", conversationId, userMessageId);

            _logger.LogInformation("Fire-and-forget: Sending message {MessageId} to conversation {ConversationId} for process {ProcessName}, correlated to user message {UserMessageId}",
                chatMessageDto.Id, conversationId, processName, userMessageId);

            // Fire and forget - don't wait for response
            await conversationGrain.ProcessMessageAsync(chatMessageDto);

            _logger.LogInformation("Message sent successfully to process {ProcessName}", processName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to backend process {ProcessName}", processName);
        }
    }

    private async Task<Guid> GetOrCreateConversationForProcessAsync(string processName)
    {
        if (_processToBackendConversation.TryGetValue(processName, out var existing)) { return existing; }
        var id = Guid.NewGuid();
        var conv = GrainFactory.GetGrain<IConversationGrain>(id);
        var backendPrompt = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowBackendConversationSystemPrompt);
        await conv.InitializeAsync(processName, backendPrompt);

        // Set user context so backend conversations can authenticate MCP tool calls
        if (!string.IsNullOrWhiteSpace(_state.State.ProviderSubjectId))
        {
            await conv.SetUserContextAsync(_state.State.ProviderSubjectId);
        }

        await RegisterBackendConversationMappingAsync_Internal(id, processName);
        _processToBackendConversation[processName] = id;
        if (!_state.State.ActiveBackendConversationIds.Contains(id))
        {
            _state.State = _state.State with { ActiveBackendConversationIds = _state.State.ActiveBackendConversationIds.Append(id).ToList() };
            await _state.WriteStateAsync();
        }
        return id;
    }

    private async Task RegisterBackendConversationMappingAsync_Internal(Guid backendConversationId, string processName)
    {
        try
        {
            var registry = GrainFactory.GetGrain<IFlowBackendConversationRegistryGrain>(Guid.Empty);
            await registry.RegisterAsync(backendConversationId, _state.State.SessionId, processName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Registry mapping failed for backend {Backend}", backendConversationId);
        }
    }
    #endregion

    #region Streaming Aggregation Handling
    private async Task OnFlowBackendConversationUpdate(FlowBackendConversationUpdate update, StreamSequenceToken token)
    {
        try
        {
            _logger.LogInformation("Received backend conversation update for session {SessionId}, message {MessageId}, complete: {IsComplete}",
                update.FlowSessionId, update.ChatMessageDto.Id, update.IsComplete);

            _logger.LogInformation("Stream update received - Source: {Source}, State: {State}, ProcessName: {ProcessName}, ConversationId: {ConversationId}, MessageId: {MessageId}",
                update.ChatMessageDto.Source, update.ChatMessageDto.State, update.DocumentProcessName, update.BackendConversationId, update.ChatMessageDto.Id);

            if (update.FlowSessionId != _state.State.SessionId)
            {
                _logger.LogWarning("Received update for different session {ReceivedSessionId}, expected {ExpectedSessionId}",
                    update.FlowSessionId, _state.State.SessionId);
                return;
            }

            // Check if this update is for an MCP request
            // If so, track completion but don't emit SignalR updates
            if (_backendToMcpRequestMap.TryGetValue(update.BackendConversationId, out var mcpRequestId))
            {
                _logger.LogInformation("Update is for MCP request {RequestId}, tracking completion without SignalR",
                    mcpRequestId);

                await HandleMcpBackendUpdate(mcpRequestId, update);
                return; // Skip all SignalR emission for MCP requests
            }

            // Find the correct message state based on backend conversation ID
            // The update contains the backend conversation ID in ChatMessageDto.ConversationId
            var messageState = FindMessageStateByBackendConversation(update.BackendConversationId);
            if (messageState == null)
            {
                _logger.LogWarning("Could not find message state for backend conversation {BackendConversationId}", update.BackendConversationId);
                return;
            }

            // Only process assistant messages for aggregation
            if (update.ChatMessageDto.Source == ChatMessageSource.Assistant)
            {
                // Update aggregation sections for this specific message
                var proc = string.IsNullOrWhiteSpace(update.DocumentProcessName) ? "unknown" : update.DocumentProcessName;
                _logger.LogInformation("Updating aggregation section for process {ProcessName}, complete: {IsComplete}, user message {UserMessageId}, content length: {ContentLength}",
                    proc, update.IsComplete, messageState.MessageId, update.ChatMessageDto.Message?.Length ?? 0);
                messageState.AggregationSections[proc] = (update.ChatMessageDto.Message ?? string.Empty, update.IsComplete);
                await UpsertAggregationMessageAsync(messageState);
            }
            else
            {
                _logger.LogDebug("Skipping non-assistant message from backend: Source={Source}", update.ChatMessageDto.Source);
            }

            if (!messageState.FinalSynthesisEmitted && messageState.AggregationSections.Count > 0 && messageState.AggregationSections.Values.All(s => s.Complete))
            {
                _logger.LogInformation("All {SectionCount} aggregation sections complete for message {MessageId}, emitting final synthesis",
                    messageState.AggregationSections.Count, messageState.MessageId);
                await EmitFinalSynthesisAsync(messageState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling backend update in Flow session {SessionId}", _state.State.SessionId);
        }
    }

    private async Task UpsertAggregationMessageAsync(MessageAggregationState messageState)
    {
        var now = DateTime.UtcNow;
        var composite = BuildAggregationComposite(messageState.AggregationSections, false);
        var lengthDelta = Math.Abs(composite.Length - messageState.LastAggregationLength);
        if (messageState.AggregationMessageId == null)
        {
            var msg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = _state.State.SessionId,
                Source = ChatMessageSource.Assistant,
                Message = composite,
                CreatedUtc = now,
                ModifiedUtc = now
            };
            messageState.AggregationMessageId = msg.Id;
            _logger.LogInformation("Creating new aggregation message {AggregationId} for user message {UserMessageId} with initial content length {Length}",
                msg.Id, messageState.MessageId, composite.Length);
            _state.State = _state.State with { UserConversationMessages = _state.State.UserConversationMessages.Append(msg).ToList(), LastActivityUtc = now };
            await _state.WriteStateAsync();
            await PushAggregationSignalRAsync(msg, inProgress: true, lastUpdate: composite, aggregationSections: messageState.AggregationSections);
            // Emit initial flow status notification for UI status bar
            try
            {
                var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                var status = new ChatMessageStatusNotification(msg.Id, "Flow: assembling responses from relevant processes...")
                {
                    ProcessingComplete = false,
                    Persistent = false
                };
                await notifier.NotifyChatMessageStatusAsync(status);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed sending initial flow status notification");
            }
            messageState.LastAggregationPushUtc = now;
            messageState.LastAggregationLength = composite.Length;
            return;
        }
        // Throttle
        if ((now - messageState.LastAggregationPushUtc) < AggregationPushMinInterval && lengthDelta < AggregationMinLengthDelta)
        {
            return;
        }
        var list = _state.State.UserConversationMessages.ToList();
        var existing = list.FirstOrDefault(m => m.Id == messageState.AggregationMessageId);
        if (existing != null && !messageState.FinalSynthesisEmitted)
        {
            existing.Message = composite;
            existing.ModifiedUtc = now;
            _state.State = _state.State with { UserConversationMessages = list, LastActivityUtc = now };
            await _state.WriteStateAsync();
            await PushAggregationSignalRAsync(existing, inProgress: true, lastUpdate: composite, aggregationSections: messageState.AggregationSections);
            messageState.LastAggregationPushUtc = now;
            messageState.LastAggregationLength = composite.Length;
        }
    }

    private async Task OnFlowBackendStatusUpdate(FlowBackendStatusUpdate update, StreamSequenceToken token)
    {
        try
        {
            _logger.LogDebug("Received backend status update for session {SessionId}, backend {BackendId}: {Status}",
                update.FlowSessionId, update.BackendConversationId, update.StatusMessage);

            if (update.FlowSessionId != _state.State.SessionId)
            {
                _logger.LogWarning("Received status update for different session {ReceivedSessionId}, expected {ExpectedSessionId}",
                    update.FlowSessionId, _state.State.SessionId);
                return;
            }

            // Find the correct message state based on correlation
            var messageState = FindMessageStateByBackendConversation(update.BackendConversationId);
            if (messageState == null)
            {
                _logger.LogWarning("Could not find message state for backend conversation {BackendConversationId}", update.BackendConversationId);
                return;
            }

            // Collect the status message for later synthesis
            messageState.CollectedStatusMessages.Add(new BackendStatusMessage
            {
                DocumentProcessName = update.DocumentProcessName,
                StatusMessage = update.StatusMessage,
                Timestamp = update.Timestamp,
                IsComplete = update.IsProcessingComplete,
                IsPersistent = update.IsPersistent
            });

            // Periodically synthesize and send status updates
            await SynthesizeStatusMessagesAsync(messageState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing backend status update");
        }
    }

    private async Task EmitFinalSynthesisAsync(MessageAggregationState messageState)
    {
        messageState.FinalSynthesisEmitted = true;
        var synthesis = await SynthesizeResponseAsync(string.Empty, messageState.AggregationSections.ToDictionary(k => k.Key, v => v.Value.Text));
        var list = _state.State.UserConversationMessages.ToList();

        // Keep the intermediate aggregation message but mark it as complete
        var agg = list.FirstOrDefault(m => m.Id == messageState.AggregationMessageId);
        if (agg != null)
        {
            // Update the aggregation message to its final state
            agg.Message = BuildAggregationComposite(messageState.AggregationSections, true);
            agg.ModifiedUtc = DateTime.UtcNow;
            _logger.LogInformation("Finalizing intermediate aggregation message {AggregationId} for user message {UserMessageId}", agg.Id, messageState.MessageId);
        }

        // Create the final synthesis message
        var final = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = _state.State.SessionId,
            Source = ChatMessageSource.Assistant,
            Message = synthesis,
            CreatedUtc = DateTime.UtcNow,
            ModifiedUtc = DateTime.UtcNow,
            ReplyToChatMessageId = messageState.MessageId // Reply to the original user message
        };
        list.Add(final);

        // Track the superseding relationship - the aggregation is superseded by the final
        var supersededMessages = _state.State.SupersededMessages.ToDictionary(k => k.Key, v => v.Value);
        if (agg != null)
        {
            supersededMessages[agg.Id] = final.Id;
        }

        _state.State = _state.State with
        {
            UserConversationMessages = list,
            LastActivityUtc = DateTime.UtcNow,
            SupersededMessages = supersededMessages
        };
        await _state.WriteStateAsync();

        // Send SignalR notifications - aggregation marked as superseded, final message sent
        if (agg != null)
        {
            await PushAggregationSignalRAsync(agg, inProgress: false, lastUpdate: agg.Message ?? string.Empty,
                aggregationSections: messageState.AggregationSections, supersededBy: final.Id);
        }
        await PushFinalSignalRAsync(final, synthesis, messageState.AggregationSections, supersedingMessageId: agg?.Id);

        // Send completion status to clear the status bar
        try
        {
            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            var completionStatus = new ChatMessageStatusNotification(
                messageState.AggregationMessageId ?? messageState.MessageId,
                "Flow synthesis complete")
            {
                ProcessingComplete = true,
                Persistent = false
            };
            await notifier.NotifyChatMessageStatusAsync(completionStatus);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed sending completion status notification");
        }
    }

    private string BuildAggregationComposite(Dictionary<string, (string Text, bool Complete)> aggregationSections, bool finalizing)
    {
        var sb = new StringBuilder();
        if (!finalizing)
        {
            var complete = aggregationSections.Count == 0 ? 0 : aggregationSections.Count(s => s.Value.Complete);
            sb.AppendLine($"Assembling responses from document process backends… ({complete}/{aggregationSections.Count} complete)");
        }
        else
        {
            sb.AppendLine("Final aggregation (all backend conversations complete). Original intermediate details retained.");
        }
        foreach (var kv in aggregationSections.OrderBy(k => k.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"## {kv.Key}");
            sb.AppendLine(kv.Value.Text);
            if (!kv.Value.Complete) { sb.AppendLine("(in progress)"); }
        }
        if (!finalizing)
        {
            sb.AppendLine();
            sb.AppendLine("(Will synthesize final response when all backends complete)");
        }
        return sb.ToString();
    }

    private string BuildAggregationExtraDataJson(Dictionary<string, (string Text, bool Complete)> aggregationSections) => JsonSerializer.Serialize(new
    {
        sections = aggregationSections.Select(s => new { process = s.Key, text = s.Value.Text, complete = s.Value.Complete }).OrderBy(s => s.process).ToList()
    });
    #endregion

    #region SignalR Notification Helpers
    private ChatMessageDTO MapToDto(ChatMessage msg) => new()
    {
        Id = msg.Id,
        ConversationId = msg.ConversationId,
        Source = msg.Source,
        Message = msg.Message,
        ContentText = msg.ContentText,
        CreatedUtc = msg.CreatedUtc,
        ReplyToId = msg.ReplyToChatMessageId,
        // Only set UserId for user messages, not assistant messages
        UserId = msg.Source == ChatMessageSource.User ? (msg.UserId ?? _state.State.ProviderSubjectId) : null,
        UserFullName = msg.Source == ChatMessageSource.User ? (_state.State.UserName ?? "User") : "AI Assistant"
    };

    private async Task PushUserMessageSignalRAsync(ChatMessage userMessage)
    {
        try
        {
            var dto = MapToDto(userMessage);
            // UserId and UserFullName are now correctly set in MapToDto based on message source

            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            // Use ChatMessageResponseReceived for user messages in Flow
            var evt = new ChatMessageResponseReceived(_state.State.SessionId, dto, string.Empty);
            await notifier.NotifyChatMessageResponseReceivedAsync(evt);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "User message SignalR push failed");
        }
    }

    private async Task PushAggregationSignalRAsync(ChatMessage aggregationEntity, bool inProgress, string lastUpdate, Dictionary<string, (string Text, bool Complete)> aggregationSections, Guid? supersededBy = null)
    {
        try
        {
            var dto = MapToDto(aggregationEntity);
            dto.IsFlowAggregation = true;
            dto.IsIntermediate = inProgress;
            dto.SupersededByMessageId = supersededBy;
            dto.ExtraDataJson = BuildAggregationExtraDataJson(aggregationSections);
            dto.State = inProgress ? ChatMessageCreationState.InProgress : ChatMessageCreationState.Complete;
            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            var evt = new ChatMessageResponseReceived(_state.State.SessionId, dto, lastUpdate ?? string.Empty);
            await notifier.NotifyChatMessageResponseReceivedAsync(evt);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Aggregation SignalR push failed");
        }
    }

    private async Task PushFinalSignalRAsync(ChatMessage finalEntity, string lastUpdate, Dictionary<string, (string Text, bool Complete)> aggregationSections, Guid? supersedingMessageId = null)
    {
        try
        {
            var dto = MapToDto(finalEntity);
            dto.IsFlowAggregation = true;
            dto.IsIntermediate = false;
            dto.ExtraDataJson = BuildAggregationExtraDataJson(aggregationSections);
            dto.State = ChatMessageCreationState.Complete;
            // The final message doesn't get marked as superseded - it supersedes the aggregation message
            // The aggregation message gets marked with SupersededByMessageId pointing to this final message
            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await notifier.NotifyChatMessageResponseReceivedAsync(new ChatMessageResponseReceived(_state.State.SessionId, dto, lastUpdate));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Final synthesis SignalR push failed");
        }
    }
    #endregion

    #region Synthesis and Direct Response
    private async Task<string> SynthesizeResponseAsync(string originalMessage, Dictionary<string, string> conversationResults)
    {
        if (!conversationResults.Any())
        {
            return "I'm sorry, I wasn't able to process your request at this time.";
        }

        if (conversationResults.Count == 1)
        {
            return conversationResults.Values.First();
        }

        // Use LLM-based synthesis with FlowResponseSynthesisPrompt for multiple responses
        try
        {
            _logger.LogInformation("Synthesizing {Count} responses using FlowResponseSynthesisPrompt", conversationResults.Count);

            // Build the responses section for the prompt
            var responsesText = new StringBuilder();
            foreach (var kvp in conversationResults.OrderBy(k => k.Key))
            {
                responsesText.AppendLine($"Document Process: {kvp.Key}");
                responsesText.AppendLine("Response:");
                responsesText.AppendLine(kvp.Value);
                responsesText.AppendLine();
            }

            // Render the synthesis prompt with variables
            var synthesisPrompt = await RenderFlowResponseSynthesisPromptAsync(originalMessage, responsesText.ToString());

            // Get Flow kernel and generate synthesis
            var kernel = await _kernelFactory.GetFlowKernelAsync(_state.State.ProviderSubjectId);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddUserMessage(synthesisPrompt);

            var executionSettings = await _kernelFactory.GetFlowPromptExecutionSettingsAsync();
            var response = await chatService.GetChatMessageContentAsync(history, executionSettings);

            var synthesizedResponse = response.Content ?? string.Empty;
            _logger.LogInformation("Response synthesis complete, length: {Length} characters", synthesizedResponse.Length);

            return synthesizedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to synthesize responses using LLM, falling back to simple concatenation");

            // Fallback to simple concatenation
            var sb = new StringBuilder();
            sb.AppendLine("Here's what I found:");
            foreach (var kv in conversationResults)
            {
                sb.AppendLine($"\n**From {kv.Key}:**");
                sb.AppendLine(kv.Value);
            }
            return sb.ToString();
        }
    }

    private async Task<string> GenerateDirectFlowResponseAsync(string message)
    {
        try
        {
            _logger.LogInformation("Generating direct Flow response for message: {Message}", message);

            // Get Flow's dedicated kernel instance
            var kernel = await _kernelFactory.GetFlowKernelAsync(_state.State.ProviderSubjectId);

            // Build conversation history from previous Flow messages (use configured history limit)
            var chatHistoryString = CreateFlowChatHistoryString(_state.State.UserConversationMessages, FlowOptions.ConversationHistoryLimit);

            // Use Flow system prompt for conversational responses
            var defaultFlowPrompt = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowUserConversationSystemPrompt);
            var flowSystemPrompt = _state.State.SystemPrompt ?? defaultFlowPrompt;

            // Load the conversational fallback prompt from system prompt service
            var conversationalFallbackPrompt = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowConversationalFallbackPrompt);

            // Build user prompt for direct conversation (with conversational fallback prompt)
            var userPrompt = await BuildFlowUserPromptAsync(chatHistoryString, message, conversationalFallbackPrompt);

            // Generate response using chat completion
            var chatService = kernel.GetRequiredService<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            var history = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
            history.AddSystemMessage(flowSystemPrompt);
            history.AddUserMessage(userPrompt);

            // Get Flow-specific execution settings (uses Flow configured model + ChatReplies task type)
            var executionSettings = await _kernelFactory.GetFlowPromptExecutionSettingsAsync();

            var response = await chatService.GetChatMessageContentAsync(history, executionSettings, kernel);

            _logger.LogInformation("Generated direct Flow response: {ResponseLength} characters", response.Content?.Length ?? 0);
            return response.Content ?? "I'm here to help! Could you please tell me more about what you'd like to know?";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating direct Flow response");
            return "I'm here to help! I can assist with various topics and will learn about your specific needs as we chat. What would you like to know?";
        }
    }

    private string CreateFlowChatHistoryString(List<ChatMessage> messages, int numberOfMessagesToInclude = int.MaxValue)
    {
        if (!messages.Any()) return "";

        var chatHistory = numberOfMessagesToInclude == int.MaxValue
            ? messages.OrderBy(x => x.CreatedUtc).ToList()
            : messages.OrderByDescending(x => x.CreatedUtc).Take(numberOfMessagesToInclude).OrderBy(x => x.CreatedUtc).ToList();

        var historyBuilder = new StringBuilder();
        foreach (var message in chatHistory)
        {
            historyBuilder
                .AppendLine($"role:{message.Source}")
                .AppendLine($"content:{message.Message}");
        }
        return historyBuilder.ToString();
    }

    private async Task<string> BuildFlowUserPromptAsync(string chatHistoryString, string userMessage, string? conversationalPrompt = null)
    {
        // Simple conversational prompt for direct Flow responses
        var template = @"
You are an AI assistant that can help with a wide variety of topics. You have access to various tools and capabilities.

{{if conversational_prompt}}
{{conversational_prompt}}

{{end}}
{{if chat_history_string}}
## Previous Conversation:
{{chat_history_string}}
{{end}}

## Current Message:
{{user_message}}

Please provide a helpful, conversational response. If the user's request seems to relate to specific document processes or specialized knowledge areas, you can mention that you can engage additional specialized capabilities as needed.
";

        var scribanTemplate = Scriban.Template.Parse(template);
        return await scribanTemplate.RenderAsync(new
        {
            conversational_prompt = conversationalPrompt,
            chat_history_string = chatHistoryString,
            user_message = userMessage
        }, member => member.Name);
    }

    private async Task SendDirectFlowResponseAsync(string response)
    {
        try
        {
            _logger.LogInformation("Sending direct Flow response via SignalR to conversation {ConversationId}", _state.State.SessionId);

            // Create a response message for the direct Flow response
            var assistantMessage = new ChatMessageDTO
            {
                Id = Guid.NewGuid(),
                ConversationId = _state.State.SessionId, // Use the Flow session ID as conversation ID
                Source = ChatMessageSource.Assistant,
                Message = response,
                CreatedUtc = DateTime.UtcNow,
                State = ChatMessageCreationState.Complete,
                IsFlowAggregation = false, // This is a direct response, not an aggregation
                UserId = "flow-system",
                UserFullName = "Flow Assistant"
            };

            // Store the response in Flow's own conversation history
            var assistantChatMessage = new ChatMessage
            {
                Id = assistantMessage.Id,
                ConversationId = _state.State.SessionId,
                Source = ChatMessageSource.Assistant,
                Message = response,
                CreatedUtc = DateTime.UtcNow,
                ModifiedUtc = DateTime.UtcNow
            };

            _state.State = _state.State with
            {
                UserConversationMessages = _state.State.UserConversationMessages.Append(assistantChatMessage).ToList(),
                LastActivityUtc = DateTime.UtcNow
            };

            // Send SignalR notification directly to the conversation ID (not backend conversations)
            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            var notification = new ChatMessageResponseReceived(_state.State.SessionId, assistantMessage, response);
            await notifier.NotifyChatMessageResponseReceivedAsync(notification);

            _logger.LogInformation("Direct Flow response sent successfully to conversation {ConversationId}", _state.State.SessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending direct Flow response to conversation {ConversationId}", _state.State.SessionId);
        }
    }
    #endregion

    #region Background Task
    private async Task ProcessStreamingMessageInBackgroundAsync(string taskId, string message, string context, CancellationToken token)
    {
        try
        {
            var result = await ProcessMessageForMcpAsync(message, context);
            if (!token.IsCancellationRequested)
            {
                _currentStatus = FlowSessionStatus.Completed;
                _currentResponse = result.Response;
            }
        }
        catch (OperationCanceledException) { _currentStatus = FlowSessionStatus.Cancelled; }
        catch (Exception ex) { _currentStatus = FlowSessionStatus.Error; _logger.LogError(ex, "Async Flow processing failed"); }
        finally
        {
            _activeProcessingTasks.Remove(taskId);
            if (_currentProcessingTaskId == taskId) { _currentProcessingTaskId = null; }
        }
    }
    #endregion

    #region Interface Methods (Engage/Disengage/Tools)
    public async Task<List<string>> EngageDocumentProcessAsync(string documentProcessName)
    {
        if (string.IsNullOrWhiteSpace(documentProcessName)) { return new List<string>(); }
        if (_state.State.EngagedDocumentProcesses.Contains(documentProcessName)) { return new List<string>(); }
        _state.State = _state.State with { EngagedDocumentProcesses = _state.State.EngagedDocumentProcesses.Append(documentProcessName).ToList() };
        // Placeholder plugin list; future: dynamic discovery
        var plugins = new List<string> { "GeneralChatPlugin" };
        _state.State = _state.State with { AvailablePlugins = _state.State.AvailablePlugins.Union(plugins).ToHashSet() };
        await _state.WriteStateAsync();
        return plugins;
    }

    public Task<Dictionary<string, object>> GetAvailableToolsAsync()
    {
        var tools = _state.State.AvailablePlugins.ToDictionary(p => p, p => (object)new { Name = p, Capabilities = new[] { "chat", "flow" } });
        return Task.FromResult(tools);
    }

    public async Task DisengageDocumentProcessAsync(string documentProcessName)
    {
        if (!_state.State.EngagedDocumentProcesses.Contains(documentProcessName)) { return; }
        _state.State = _state.State with { EngagedDocumentProcesses = _state.State.EngagedDocumentProcesses.Where(p => !string.Equals(p, documentProcessName, StringComparison.OrdinalIgnoreCase)).ToList() };
        await _state.WriteStateAsync();
    }
    #endregion

    #region Vector Intent Logic
    private Task<List<string>> DetermineDocumentProcessesAsync(string message, string context) => FlowDetermineDocumentProcessesAsync(message, context);

    private async Task<List<string>> FlowDetermineDocumentProcessesAsync(string message, string context)
    {
        try
        {
            var availableProcesses = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
            if (!availableProcesses.Any()) return new List<string>();
            if (availableProcesses.Count == 1) return new List<string> { availableProcesses[0].ShortName };
            var selected = await DetermineDocumentProcessesByVectorSimilarityAsync(message, availableProcesses);
            if (selected.Any()) return selected;
            return availableProcesses.Take(2).Select(p => p.ShortName).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Flow intent detection failure - using fallback");
            var fallback = await DetermineDocumentProcessesByVectorFallbackAsync(message);
            if (!fallback.Any())
            {
                _logger.LogInformation("No specific document process intent detected - Flow will respond conversationally");
                return new List<string>(); // Return empty list - Flow will handle conversationally
            }
            return fallback;
        }
    }

    private async Task<List<string>> DetermineDocumentProcessesByVectorSimilarityAsync(string message, List<DocumentProcessInfo> availableProcesses)
    {
        var results = new List<string>();
        var processRelevanceScores = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Ensure metadata indexed
            await EnsureDocumentProcessMetadataIsIndexedAsync(availableProcesses);

            var synthetic = new DocumentProcessInfo
            {
                Id = Guid.NewGuid(),
                ShortName = SystemIndexes.DocumentProcessMetadataIntentIndex,
                Description = "Intent detection synthetic process index",
                BlobStorageContainerName = "system-metadata",
                LogicType = DocumentProcessLogicType.SemanticKernelVectorStore,
                VectorStoreChunkSize = 150,
                VectorStoreChunkOverlap = 0,
                VectorStoreChunkingMode = Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Simple,
                Repositories = new List<string>()
            };
            var repo = await _documentRepositoryFactory.CreateForDocumentProcessAsync(synthetic);

            // Use the configured minimum relevance threshold
            var minRelevance = FlowOptions.RequireMinimumRelevanceForEngagement
                ? FlowOptions.MinimumIntentRelevanceThreshold
                : 0.0;

            var searchOptions = new ConsolidatedSearchOptions
            {
                DocumentLibraryType = DocumentLibraryType.PrimaryDocumentProcessLibrary,
                IndexName = SystemIndexes.DocumentProcessMetadataIntentIndex,
                Top = 5,
                MinRelevance = Math.Min(0.2, minRelevance), // Use lower of 0.2 or configured to allow initial search
                EnableProgressiveSearch = true,
                EnableKeywordFallback = true,
                ProgressiveRelevanceThresholds = new double[] { 0.6, 0.45, 0.3, 0.2 }
            };

            var searchResults = await repo.SearchAsync(SystemIndexes.DocumentProcessMetadataIntentIndex, message, searchOptions);

            foreach (var r in searchResults.Take(5)) // Check more results to find ones above threshold
            {
                var candidate = (r.Description ?? r.SourceReferenceLink) ?? string.Empty;
                var idx = candidate.IndexOf("process-");
                if (idx >= 0)
                {
                    var seg = candidate.Substring(idx).Replace("process-", string.Empty);
                    if (seg.Contains('.')) seg = seg.Split('.')[0];
                    if (!string.IsNullOrWhiteSpace(seg) && availableProcesses.Any(p => p.ShortName.Equals(seg, StringComparison.OrdinalIgnoreCase)))
                    {
                        // Track the relevance score for this process
                        // Check if this is a VectorStoreSourceReferenceItem with a Score property
                        double score = 0.5; // Default score if not available
                        if (r is VectorStoreSourceReferenceItem vsItem)
                        {
                            score = vsItem.Score;
                        }
                        else if (r is VectorStoreAggregatedSourceReferenceItem vsAggItem)
                        {
                            score = vsAggItem.Score;
                        }

                        if (!processRelevanceScores.ContainsKey(seg) || processRelevanceScores[seg] < score)
                        {
                            processRelevanceScores[seg] = score;
                        }
                    }
                }
            }

            // Filter results based on minimum relevance threshold
            foreach (var kvp in processRelevanceScores)
            {
                _logger.LogInformation("Document process '{ProcessName}' has relevance score {Score:F3} (threshold: {Threshold:F3})",
                    kvp.Key, kvp.Value, FlowOptions.MinimumIntentRelevanceThreshold);

                if (kvp.Value >= FlowOptions.MinimumIntentRelevanceThreshold)
                {
                    results.Add(kvp.Key);
                    _logger.LogInformation("Document process '{ProcessName}' MEETS threshold and will be engaged", kvp.Key);
                }
                else if (!FlowOptions.RequireMinimumRelevanceForEngagement && processRelevanceScores.Count == 1)
                {
                    // If we don't require minimum relevance and there's only one process available, use it
                    results.Add(kvp.Key);
                    _logger.LogInformation("Document process '{ProcessName}' below threshold but using anyway (only process available and RequireMinimumRelevance=false)", kvp.Key);
                }
                else
                {
                    _logger.LogInformation("Document process '{ProcessName}' BELOW threshold and will NOT be engaged", kvp.Key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector similarity failure - fallback will apply if empty");
        }

        // Log the decision
        if (results.Count == 0)
        {
            _logger.LogInformation("No document processes met the minimum relevance threshold of {Threshold:F3} - Flow will respond conversationally",
                FlowOptions.MinimumIntentRelevanceThreshold);
        }
        else
        {
            _logger.LogInformation("Vector similarity found {ResultCount} processes above threshold: {Processes}",
                results.Count, string.Join(", ", results));
        }

        return results;
    }

    private async Task EnsureDocumentProcessMetadataIsIndexedAsync(List<DocumentProcessInfo> processes)
    {
        try
        {
            if (!processes.Any()) return;
            var synthetic = new DocumentProcessInfo
            {
                Id = Guid.NewGuid(),
                ShortName = SystemIndexes.DocumentProcessMetadataIntentIndex,
                Description = "Intent detection synthetic process index",
                BlobStorageContainerName = "system-metadata",
                LogicType = DocumentProcessLogicType.SemanticKernelVectorStore,
                VectorStoreChunkSize = 150,
                VectorStoreChunkOverlap = 0,
                VectorStoreChunkingMode = Microsoft.Greenlight.Shared.Enums.TextChunkingMode.Simple,
                Repositories = new List<string>()
            };
            var repo = await _documentRepositoryFactory.CreateForDocumentProcessAsync(synthetic);
            var tasks = processes.Select(p => IndexDocumentProcessMetadataAsync(repo, p));
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Metadata indexing skipped due to error");
        }
    }

    private async Task IndexDocumentProcessMetadataAsync(IDocumentRepository repo, DocumentProcessInfo process)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Document Process: {process.ShortName}");
            if (!string.IsNullOrWhiteSpace(process.Description)) sb.AppendLine($"Description: {process.Description}");
            if (!string.IsNullOrWhiteSpace(process.OutlineText)) sb.AppendLine($"Outline: {process.OutlineText}");
            sb.AppendLine($"LogicType: {process.LogicType}");
            sb.AppendLine($"Citations: {process.NumberOfCitationsToGetFromRepository}");
            if (process.Repositories?.Any() == true) sb.AppendLine($"Repositories: {string.Join(", ", process.Repositories)}");
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            using var ms = new MemoryStream(bytes);
            var fileName = $"process-{process.ShortName}.metadata";
            var tags = new Dictionary<string, string>
            {
                ["documentProcessId"] = process.Id.ToString(),
                ["documentProcessName"] = process.ShortName,
                ["metadataType"] = "intent-detection",
                ["systemIndex"] = "true"
            };
            await repo.StoreContentAsync(SystemIndexes.DocumentProcessMetadataIntentIndex, SystemIndexes.DocumentProcessMetadataIntentIndex, ms, fileName, process.Id.ToString(), "system", tags);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Index metadata failure {Process}", process.ShortName);
        }
    }

    private async Task<List<string>> DetermineDocumentProcessesByVectorFallbackAsync(string message)
    {
        try
        {
            var processes = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
            if (!processes.Any()) return new List<string>();
            if (processes.Count == 1) return new List<string> { processes[0].ShortName };

            // Use LLM-based intent detection with FlowIntentDetectionPrompt
            _logger.LogInformation("Vector fallback: Using LLM-based intent detection for {ProcessCount} available processes", processes.Count);

            // Build available processes description
            var availableProcessesText = new StringBuilder();
            foreach (var process in processes)
            {
                availableProcessesText.AppendLine($"- {process.ShortName}: {process.Description}");
            }

            // Render the intent detection prompt
            var intentPrompt = await RenderFlowIntentDetectionPromptAsync(message, availableProcessesText.ToString());

            // Use Flow kernel to analyze intent
            var kernel = await _kernelFactory.GetFlowKernelAsync(_state.State.ProviderSubjectId);
            var chatService = kernel.GetRequiredService<IChatCompletionService>();
            var history = new ChatHistory();
            history.AddUserMessage(intentPrompt);

            var executionSettings = await _kernelFactory.GetFlowPromptExecutionSettingsAsync();
            var response = await chatService.GetChatMessageContentAsync(history, executionSettings);

            var llmResponse = response.Content ?? string.Empty;
            _logger.LogInformation("LLM intent detection response: {Response}", llmResponse);

            // Parse process names from LLM response (expecting comma-separated list or line-by-line)
            var recommendedProcesses = new List<string>();
            var lines = llmResponse.Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var cleaned = line.Trim().TrimStart('-', '*', '•').Trim();
                // Match against available process names (case-insensitive)
                var match = processes.FirstOrDefault(p =>
                    cleaned.Equals(p.ShortName, StringComparison.OrdinalIgnoreCase) ||
                    cleaned.Contains(p.ShortName, StringComparison.OrdinalIgnoreCase));

                if (match != null && !recommendedProcesses.Contains(match.ShortName, StringComparer.OrdinalIgnoreCase))
                {
                    recommendedProcesses.Add(match.ShortName);
                    _logger.LogInformation("LLM recommended document process: {ProcessName}", match.ShortName);
                }
            }

            // If LLM didn't recommend any specific processes, return empty list (will trigger conversational fallback)
            if (!recommendedProcesses.Any())
            {
                _logger.LogInformation("LLM did not recommend any specific document processes");
                return new List<string>();
            }

            return recommendedProcesses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM-based intent detection fallback failed");
            // Final fallback: return first 2 processes to avoid total failure
            try
            {
                var processes = await _documentProcessInfoService.GetCombinedDocumentProcessInfoListAsync();
                return processes.Take(2).Select(p => p.ShortName).ToList();
            }
            catch
            {
                return new List<string>();
            }
        }
    }
    #endregion

    #region Message State Management Helpers
    private MessageAggregationState? GetMessageState(Guid userMessageId)
    {
        return _messageStates.TryGetValue(userMessageId, out var state) ? state : null;
    }

    private MessageAggregationState? FindMessageStateByBackendConversation(Guid backendConversationId)
    {
        // First try the correlation map
        if (_backendToUserMessageMap.TryGetValue(backendConversationId, out var userMessageId))
        {
            return GetMessageState(userMessageId);
        }

        // Fallback: search through all message states
        return _messageStates.Values.FirstOrDefault(state =>
            state.AggregationSections.Keys.Any(key =>
                key.Contains(backendConversationId.ToString(), StringComparison.OrdinalIgnoreCase)));
    }

    private void CleanupOldMessageStates()
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddHours(-2); // Keep states for 2 hours
            var statesToRemove = _messageStates
                .Where(kvp => kvp.Value.FinalSynthesisEmitted && kvp.Value.LastAggregationPushUtc < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var messageId in statesToRemove)
            {
                if (_messageStates.TryRemove(messageId, out _))
                {
                    // Also clean up correlation map
                    var correlationsToRemove = _backendToUserMessageMap
                        .Where(kvp => kvp.Value == messageId)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    foreach (var backendId in correlationsToRemove)
                    {
                        _backendToUserMessageMap.TryRemove(backendId, out _);
                    }
                }
            }

            if (statesToRemove.Any())
            {
                _logger.LogDebug("Cleaned up {Count} old message states", statesToRemove.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error cleaning up old message states");
        }
    }

    private async Task SynthesizeStatusMessagesAsync(MessageAggregationState messageState)
    {
        try
        {
            // Only synthesize if we have status messages and haven't done so recently
            if (!messageState.CollectedStatusMessages.Any() ||
                (DateTime.UtcNow - messageState.LastStatusSynthesis).TotalSeconds < 5)
            {
                return;
            }

            // Group status messages by backend
            var groupedMessages = messageState.CollectedStatusMessages
                .GroupBy(m => m.DocumentProcessName)
                .ToList();

            // Create synthesized status message
            var statusParts = new List<string>();
            foreach (var group in groupedMessages)
            {
                var latestMessage = group.OrderByDescending(m => m.Timestamp).First();
                if (!string.IsNullOrWhiteSpace(latestMessage.StatusMessage))
                {
                    statusParts.Add($"{group.Key}: {latestMessage.StatusMessage}");
                }
            }

            if (statusParts.Any())
            {
                var synthesizedStatus = string.Join(" | ", statusParts);

                // Check if all backends have completed processing
                var allComplete = groupedMessages.All(group =>
                    group.Any(m => m.IsComplete));

                // If all complete, send a final clear status
                if (allComplete)
                {
                    synthesizedStatus = "All processes complete";
                }

                // Send synthesized status notification
                var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                var statusEvent = new ChatMessageStatusNotification(
                    messageState.AggregationMessageId ?? messageState.MessageId,
                    synthesizedStatus)
                {
                    ProcessingComplete = allComplete,
                    Persistent = false
                };

                await notifier.NotifyChatMessageStatusAsync(statusEvent);

                messageState.LastStatusSynthesis = DateTime.UtcNow;

                // Clear all messages if complete, or just non-persistent ones
                if (allComplete)
                {
                    messageState.CollectedStatusMessages.Clear();
                }
                else
                {
                    messageState.CollectedStatusMessages.RemoveAll(m => !m.IsPersistent);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error synthesizing status messages");
        }
    }
    #endregion

    #region Prompt Rendering Helpers
    /// <summary>
    /// Renders the FlowResponseSynthesisPrompt using Scriban with the provided variables.
    /// </summary>
    /// <param name="query">The original user query.</param>
    /// <param name="responses">The formatted responses from document processes.</param>
    /// <returns>The rendered prompt text.</returns>
    private async Task<string> RenderFlowResponseSynthesisPromptAsync(string query, string responses)
    {
        var promptText = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowResponseSynthesisPrompt);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            _logger.LogWarning("FlowResponseSynthesisPrompt not found, using fallback");
            return $"Synthesize the following responses into a single coherent answer for the query: {query}\n\nResponses:\n{responses}";
        }

        try
        {
            var variables = new Dictionary<string, object>
            {
                ["query"] = query,
                ["responses"] = responses
            };

            var rendered = await _systemPromptInfoService.RenderPromptAsync(PromptNames.FlowResponseSynthesisPrompt, variables);
            return rendered ?? promptText; // Fallback to unrendered if rendering fails
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render FlowResponseSynthesisPrompt, using template as-is");
            return promptText;
        }
    }

    /// <summary>
    /// Renders the FlowIntentDetectionPrompt using Scriban with the provided variables.
    /// </summary>
    /// <param name="query">The user query to analyze.</param>
    /// <param name="availableProcesses">Formatted list of available document processes.</param>
    /// <returns>The rendered prompt text.</returns>
    private async Task<string> RenderFlowIntentDetectionPromptAsync(string query, string availableProcesses)
    {
        var promptText = await _systemPromptInfoService.GetPromptAsync(PromptNames.FlowIntentDetectionPrompt);
        if (string.IsNullOrWhiteSpace(promptText))
        {
            _logger.LogWarning("FlowIntentDetectionPrompt not found, using fallback");
            return $"Analyze this query and recommend relevant document processes: {query}\n\nAvailable processes:\n{availableProcesses}";
        }

        try
        {
            var variables = new Dictionary<string, object>
            {
                ["query"] = query,
                ["availableProcesses"] = availableProcesses
            };

            var rendered = await _systemPromptInfoService.RenderPromptAsync(PromptNames.FlowIntentDetectionPrompt, variables);
            return rendered ?? promptText; // Fallback to unrendered if rendering fails
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to render FlowIntentDetectionPrompt, using template as-is");
            return promptText;
        }
    }
    #endregion

    #region Flow Task Execution Management

    /// <summary>
    /// Starts a new Flow Task execution by creating FlowTaskAgenticExecutionGrain.
    /// </summary>
    private async Task<FlowTaskMessageResult> StartFlowTaskExecutionAsync(
        ChatMessageDTO chatMessageDto,
        FlowTaskTemplateInfo template,
        bool notifyClients)
    {
        try
        {
            var executionId = Guid.NewGuid();

            // Build user context from state
            var userContext = $"User: {_state.State.UserName ?? "Unknown"}\nProvider ID: {_state.State.ProviderSubjectId}";

            _logger.LogInformation("Starting Flow Task execution {ExecutionId} for template {TemplateId}",
                executionId, template.Id);

            var flowTaskGrain = GrainFactory.GetGrain<IFlowTaskAgenticExecutionGrain>(executionId);
            var result = await flowTaskGrain.StartExecutionAsync(
                _state.State.SessionId,
                template.Id,
                chatMessageDto.Message ?? string.Empty,
                userContext);

            if (result.IsSuccess)
            {
                // Update state to track active Flow Task
                _state.State = _state.State with
                {
                    ActiveFlowTaskExecutionId = executionId,
                    ActiveFlowTaskTemplateId = template.Id
                };
                await _state.WriteStateAsync();

                // Process the initial user message to extract fields and prompt for the first requirement
                var processingResult = await flowTaskGrain.ProcessMessageAsync(chatMessageDto.Message ?? string.Empty);
                var isComplete = await flowTaskGrain.IsCompleteAsync();

                string? completionMessage = null;

                // Check if execution is complete after first message (unlikely but possible if all fields extracted)
                if (isComplete)
                {
                    _logger.LogInformation("Flow Task execution {ExecutionId} completed after initial message", executionId);
                    _state.State = _state.State with
                    {
                        ActiveFlowTaskExecutionId = null,
                        ActiveFlowTaskTemplateId = null
                    };
                }

                // Send Flow Task response back to UI (use processing result, not initial prompt)
                var cleanedMessage = StripNoDisplayContent(processingResult.ResponseMessage);
                if (!string.IsNullOrWhiteSpace(cleanedMessage))
                {
                    var assistantMessage = new ChatMessage
                    {
                        Id = Guid.NewGuid(),
                        ConversationId = _state.State.SessionId,
                        Message = cleanedMessage,
                        Source = ChatMessageSource.Assistant,
                        CreatedUtc = DateTime.UtcNow,
                        ReplyToChatMessageId = chatMessageDto.Id
                    };

                    _state.State = _state.State with
                    {
                        UserConversationMessages = _state.State.UserConversationMessages.Append(assistantMessage).ToList()
                    };
                    await _state.WriteStateAsync();

                    if (notifyClients)
                    {
                        try
                        {
                            var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                            var notification = new ChatMessageResponseReceived(
                                chatMessageDto.Id,
                                MapToChatMessageDTO(assistantMessage),
                                cleanedMessage);
                            await notifier.NotifyChatMessageResponseReceivedAsync(notification);
                        }
                        catch (Exception notifyEx)
                        {
                            _logger.LogWarning(notifyEx, "Failed to notify UI of Flow Task start");
                        }
                    }
                }
                else
                {
                    cleanedMessage = null;
                    _logger.LogDebug("Skipping Flow Task start response - message contains only [NODISPLAY] content");
                }

                _logger.LogInformation("Flow Task execution {ExecutionId} started successfully", executionId);

                return new FlowTaskMessageResult(
                    Success: true,
                    ResponseMessage: cleanedMessage,
                    State: processingResult.State,
                    FlowTaskCompleted: isComplete,
                    CompletionMessage: completionMessage);
            }
            else
            {
                _logger.LogWarning("Flow Task execution {ExecutionId} failed to start: {ErrorMessage}",
                    executionId, result.ResponseMessage);

                // Send error message to UI
                var errorMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = _state.State.SessionId,
                    Message = $"Failed to start Flow Task: {result.ResponseMessage}",
                    Source = ChatMessageSource.Assistant,
                    CreatedUtc = DateTime.UtcNow,
                    ReplyToChatMessageId = chatMessageDto.Id
                };

                _state.State = _state.State with
                {
                    UserConversationMessages = _state.State.UserConversationMessages.Append(errorMessage).ToList(),
                    ActiveFlowTaskExecutionId = null,
                    ActiveFlowTaskTemplateId = null
                };
                await _state.WriteStateAsync();

                if (notifyClients)
                {
                    try
                    {
                        var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                        var notification = new ChatMessageResponseReceived(
                            chatMessageDto.Id,
                            MapToChatMessageDTO(errorMessage),
                            errorMessage.Message ?? string.Empty);
                        await notifier.NotifyChatMessageResponseReceivedAsync(notification);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to notify UI of Flow Task error");
                    }
                }

                return new FlowTaskMessageResult(
                    Success: false,
                    ResponseMessage: errorMessage.Message,
                    State: result.State,
                    FlowTaskCompleted: false,
                    CompletionMessage: null,
                    ErrorMessage: result.ResponseMessage ?? result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting Flow Task execution");
            return new FlowTaskMessageResult(
                Success: false,
                ResponseMessage: $"Failed to start Flow Task: {ex.Message}",
                State: FlowTaskExecutionState.Failed,
                FlowTaskCompleted: false,
                CompletionMessage: null,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Routes a message to the active FlowTaskAgenticExecutionGrain.
    /// </summary>
    private async Task<FlowTaskMessageResult> RouteMessageToFlowTaskAsync(ChatMessageDTO chatMessageDto, bool notifyClients)
    {
        try
        {
            if (!_state.State.ActiveFlowTaskExecutionId.HasValue)
            {
                _logger.LogWarning("No active Flow Task execution to route message to");
                return new FlowTaskMessageResult(
                    Success: false,
                    ResponseMessage: null,
                    State: FlowTaskExecutionState.NotStarted,
                    FlowTaskCompleted: false,
                    CompletionMessage: null,
                    ErrorMessage: "No active Flow Task execution");
            }

            var executionId = _state.State.ActiveFlowTaskExecutionId.Value;

            _logger.LogInformation("Routing message to Flow Task execution {ExecutionId}",
                executionId);

            var flowTaskGrain = GrainFactory.GetGrain<IFlowTaskAgenticExecutionGrain>(executionId);
            var result = await flowTaskGrain.ProcessMessageAsync(chatMessageDto.Message ?? string.Empty);
            var isComplete = await flowTaskGrain.IsCompleteAsync();
            string? completionMessage = null;

            if (isComplete)
            {
                _logger.LogInformation("Flow Task execution {ExecutionId} completed",
                    executionId);

                string? templateName = null;
                if (_state.State.ActiveFlowTaskTemplateId.HasValue)
                {
                    try
                    {
                        var template = await _flowTaskTemplateService.GetFlowTaskTemplateByIdAsync(_state.State.ActiveFlowTaskTemplateId.Value);
                        templateName = template?.DisplayName ?? template?.Name;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get template name for completion message");
                    }
                }

                completionMessage = string.IsNullOrWhiteSpace(templateName)
                    ? "Flow Task completed successfully. What else can I help you with?"
                    : $"Flow Task '{templateName}' completed successfully. What else can I help you with?";

                var completionChatMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = _state.State.SessionId,
                    Message = completionMessage,
                    Source = ChatMessageSource.Assistant,
                    CreatedUtc = DateTime.UtcNow,
                    ReplyToChatMessageId = chatMessageDto.Id
                };

                _state.State = _state.State with
                {
                    UserConversationMessages = _state.State.UserConversationMessages.Append(completionChatMessage).ToList(),
                    ActiveFlowTaskExecutionId = null,
                    ActiveFlowTaskTemplateId = null
                };
                await _state.WriteStateAsync();

                if (notifyClients)
                {
                    try
                    {
                        var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                        var completionNotification = new ChatMessageResponseReceived(
                            chatMessageDto.Id,
                            MapToChatMessageDTO(completionChatMessage),
                            completionMessage);
                        await notifier.NotifyChatMessageResponseReceivedAsync(completionNotification);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to notify UI of Flow Task completion message");
                    }
                }
            }

            var cleanedMessage = StripNoDisplayContent(result.ResponseMessage);
            if (!string.IsNullOrWhiteSpace(cleanedMessage))
            {
                var assistantMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = _state.State.SessionId,
                    Message = cleanedMessage,
                    Source = ChatMessageSource.Assistant,
                    CreatedUtc = DateTime.UtcNow,
                    ReplyToChatMessageId = chatMessageDto.Id
                };

                _state.State = _state.State with
                {
                    UserConversationMessages = _state.State.UserConversationMessages.Append(assistantMessage).ToList()
                };
                await _state.WriteStateAsync();

                if (notifyClients)
                {
                    try
                    {
                        var notifier = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
                        var notification = new ChatMessageResponseReceived(
                            chatMessageDto.Id,
                            MapToChatMessageDTO(assistantMessage),
                            cleanedMessage);
                        await notifier.NotifyChatMessageResponseReceivedAsync(notification);
                    }
                    catch (Exception notifyEx)
                    {
                        _logger.LogWarning(notifyEx, "Failed to notify UI of Flow Task response");
                    }
                }

                return new FlowTaskMessageResult(
                    Success: result.IsSuccess,
                    ResponseMessage: cleanedMessage,
                    State: result.State,
                    FlowTaskCompleted: isComplete,
                    CompletionMessage: completionMessage,
                    ErrorMessage: result.ErrorMessage);
            }

            _logger.LogDebug("Skipping Flow Task response - message contains only [NODISPLAY] content");
            return new FlowTaskMessageResult(
                Success: result.IsSuccess,
                ResponseMessage: null,
                State: result.State,
                FlowTaskCompleted: isComplete,
                CompletionMessage: completionMessage,
                ErrorMessage: result.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing message to Flow Task execution");
            return new FlowTaskMessageResult(
                Success: false,
                ResponseMessage: $"Failed to process Flow Task message: {ex.Message}",
                State: FlowTaskExecutionState.Failed,
                FlowTaskCompleted: false,
                CompletionMessage: null,
                ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Maps a ChatMessage to ChatMessageDTO.
    /// </summary>
    private ChatMessageDTO MapToChatMessageDTO(ChatMessage message)
    {
        return new ChatMessageDTO
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            Message = message.Message,
            ContentText = message.ContentText,
            Source = message.Source,
            CreatedUtc = message.CreatedUtc,
            ReplyToId = message.ReplyToChatMessageId,
            UserId = message.UserId
        };
    }

    /// <summary>
    /// Strips [NODISPLAY] tagged content from a message.
    /// Content between [NODISPLAY] and [/NODISPLAY] tags is removed.
    /// If the entire message is wrapped in [NODISPLAY], returns empty string.
    /// </summary>
    /// <param name="message">The message to filter.</param>
    /// <returns>The message with [NODISPLAY] content removed.</returns>
    private static string StripNoDisplayContent(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        // Remove all [NODISPLAY]...[/NODISPLAY] blocks
        var result = System.Text.RegularExpressions.Regex.Replace(
            message,
            @"\[NODISPLAY\](.*?)\[/NODISPLAY\]",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return result.Trim();
    }

    private static FlowQueryResult CreateFlowTaskQueryResult(FlowTaskMessageResult result)
    {
        if (!result.Success)
        {
            var errorMessage = result.ErrorMessage ?? result.ResponseMessage ?? "Flow Task processing failed.";
            return new FlowQueryResult
            {
                Response = errorMessage,
                Status = "error",
                Error = errorMessage,
                ConversationIds = new List<Guid>()
            };
        }

        var response = result.ResponseMessage ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(result.CompletionMessage))
        {
            response = string.IsNullOrWhiteSpace(response)
                ? result.CompletionMessage!
                : $"{response}\n\n{result.CompletionMessage}";
        }

        return new FlowQueryResult
        {
            Response = response,
            Status = result.FlowTaskCompleted ? "completed" : "in-progress",
            ConversationIds = new List<Guid>()
        };
    }

    #endregion
}
