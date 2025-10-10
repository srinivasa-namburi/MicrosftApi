// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.Greenlight.Grains.Chat.Agentic;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

/// <summary>
/// Custom agent selection strategy for Flow Task conversations.
/// Routes based on execution state and message context for predictable, deterministic flow.
/// </summary>
public class FlowTaskAgentSelectionStrategy : SelectionStrategy
{
    private readonly ILogger _logger;
    private readonly string _orchestratorAgentName;
    private readonly Func<FlowTaskExecutionState> _getCurrentState;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskAgentSelectionStrategy"/> class.
    /// </summary>
    /// <param name="logger">Logger instance for diagnostic information.</param>
    /// <param name="getCurrentState">Function to retrieve the current execution state.</param>
    /// <param name="orchestratorAgentName">Name of the orchestrator agent.</param>
    public FlowTaskAgentSelectionStrategy(
        ILogger logger,
        Func<FlowTaskExecutionState> getCurrentState,
        string orchestratorAgentName = "OrchestratorAgent")
    {
        _logger = logger;
        _getCurrentState = getCurrentState;
        _orchestratorAgentName = orchestratorAgentName;
    }

    /// <summary>
    /// Selects the next agent based on execution state and conversation context.
    /// </summary>
    protected override Task<Agent> SelectAgentAsync(
        IReadOnlyList<Agent> agents,
        IReadOnlyList<ChatMessageContent> history,
        CancellationToken cancellationToken = default)
    {
        if (!agents.Any())
        {
            throw new InvalidOperationException("No agents available.");
        }

        // Find required agents
        var orchestratorAgent = agents.FirstOrDefault(a => a.Name == _orchestratorAgentName)
            ?? throw new InvalidOperationException($"Orchestrator agent '{_orchestratorAgentName}' not found.");

        var conversationAgent = agents.FirstOrDefault(a => a.Name == "ConversationAgent")
            ?? throw new InvalidOperationException("ConversationAgent not found.");

        var confirmationAgent = agents.FirstOrDefault(a => a.Name == "ConfirmationAgent");

        // Initial message - start with ConversationAgent
        if (history.Count == 0)
        {
            _logger.LogInformation("No conversation yet - starting with ConversationAgent.");
            return Task.FromResult((Agent)conversationAgent);
        }

        var lastMessage = history.Last();
        var currentState = _getCurrentState();

        _logger.LogInformation("SelectAgent: State={State}, LastMessageFrom={AuthorName}, Role={Role}",
            currentState, lastMessage.AuthorName, lastMessage.Role);

        // STATE-DRIVEN ROUTING: AwaitingConfirmation
        // When waiting for user confirmation, route user messages to ConfirmationAgent
        if (currentState == FlowTaskExecutionState.AwaitingConfirmation && lastMessage.Role == AuthorRole.User)
        {
            if (confirmationAgent == null)
            {
                throw new InvalidOperationException("ConfirmationAgent not found but state is AwaitingConfirmation.");
            }

            _logger.LogInformation("State=AwaitingConfirmation, routing user message to ConfirmationAgent");
            return Task.FromResult((Agent)confirmationAgent);
        }

        // ORCHESTRATOR ROUTING TAGS: [NEXT:AgentName]
        // If Orchestrator emitted a routing tag, follow it
        if (lastMessage.AuthorName == _orchestratorAgentName &&
            TryExtractNextAgentName(lastMessage.Content, out string? nextAgentName))
        {
            var nextAgent = agents.FirstOrDefault(a => a.Name == nextAgentName);
            if (nextAgent != null)
            {
                _logger.LogInformation("Orchestrator selected {NextAgentName} to speak next", nextAgentName);
                return Task.FromResult(nextAgent);
            }

            _logger.LogWarning("Orchestrator requested {NextAgentName} but agent not found", nextAgentName);
        }

        // DEFAULT USER MESSAGE ROUTING: ConversationAgent
        // User messages during collection phase go to ConversationAgent
        if (lastMessage.Role == AuthorRole.User)
        {
            _logger.LogInformation("User message during collection - routing to ConversationAgent");
            return Task.FromResult((Agent)conversationAgent);
        }

        // DEFAULT: Route back to Orchestrator for decision
        _logger.LogInformation("Routing to Orchestrator for next decision");
        return Task.FromResult((Agent)orchestratorAgent);
    }

    /// <summary>
    /// Extracts the next agent name from a message containing [NEXT:AgentName] tag.
    /// </summary>
    private bool TryExtractNextAgentName(string? messageContent, out string? nextAgentName)
    {
        nextAgentName = null;

        if (string.IsNullOrEmpty(messageContent))
        {
            return false;
        }

        var tagMatch = System.Text.RegularExpressions.Regex.Match(
            messageContent,
            @"\[NEXT:([^\]]+)\]",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (tagMatch.Success && tagMatch.Groups.Count > 1)
        {
            nextAgentName = tagMatch.Groups[1].Value.Trim();
            return true;
        }

        return false;
    }
}

#pragma warning restore SKEXP0001
#pragma warning restore SKEXP0110
