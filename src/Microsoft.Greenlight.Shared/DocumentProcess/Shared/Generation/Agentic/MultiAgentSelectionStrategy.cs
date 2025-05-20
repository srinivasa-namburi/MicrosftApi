// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.ChatCompletion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic
{
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

    /// <summary>
    /// Strategy for selecting the next agent in a conversation with multiple agents.
    /// This is a more flexible alternative to the RoundRobinAgentSelectionStrategy
    /// that can handle an arbitrary number of agents and orchestration patterns.
    /// </summary>
    public class MultiAgentSelectionStrategy : SelectionStrategy
    {
        private readonly ILogger _logger;
        private readonly string _orchestratorAgentName;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiAgentSelectionStrategy"/> class.
        /// </summary>
        /// <param name="logger">Logger instance for diagnostic information.</param>
        /// <param name="orchestratorAgentName">Name of the orchestrator agent that manages the conversation flow.</param>
        public MultiAgentSelectionStrategy(ILogger logger, string orchestratorAgentName = "OrchestratorAgent")
        {
            _logger = logger;
            _orchestratorAgentName = orchestratorAgentName;
        }

        /// <summary>
        /// Selects the next agent to respond in the conversation.
        /// </summary>
        /// <param name="agents">List of available agents.</param>
        /// <param name="history">Chat history so far.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The selected agent for the next conversation turn.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no agents are available or the orchestrator agent is not found.</exception>
        protected override Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (!agents.Any())
            {
                throw new InvalidOperationException("No agents available.");
            }

            // Find orchestrator agent
            var orchestratorAgent = agents.FirstOrDefault(a => a.Name == _orchestratorAgentName);
            if (orchestratorAgent == null)
            {
                throw new InvalidOperationException($"Orchestrator agent '{_orchestratorAgentName}' not found.");
            }

            // Find content agent (first non-orchestrator agent)
            var contentAgent = agents.FirstOrDefault(a => a.Name != _orchestratorAgentName);
            if (contentAgent == null)
            {
                throw new InvalidOperationException("No content agent found.");
            }

            if (history.Count == 0)
            {
                _logger.LogInformation("No conversation yet—starting with the Orchestrator.");
                return Task.FromResult(orchestratorAgent);
            }

            var lastMessage = history.Last();
            _logger.LogInformation($"Last message from: {lastMessage.AuthorName}");

            // If the last message is from the user, select the ContentAgent to start the workflow
            if (lastMessage.Role == AuthorRole.User)
            {
                _logger.LogInformation("Last message from user — selecting ContentAgent to start the workflow.");
                return Task.FromResult(contentAgent);
            }

            // Look for direction tags in the message if it's from the Orchestrator
            // These tags tell us which agent should speak next
            if (lastMessage.AuthorName == _orchestratorAgentName && 
                TryExtractNextAgentName(lastMessage.Content, out string nextAgentName))
            {
                var nextAgent = agents.FirstOrDefault(a => a.Name == nextAgentName);
                if (nextAgent != null)
                {
                    _logger.LogInformation($"Orchestrator selected {nextAgentName} to speak next.");
                    return Task.FromResult(nextAgent);
                }
            }

            // If no specific direction was given or if the last message wasn't from the orchestrator,
            // route back to the orchestrator to determine the next action
            _logger.LogInformation("Routing to Orchestrator for next decision.");
            return Task.FromResult(orchestratorAgent);
        }

        /// <summary>
        /// Attempts to extract the next agent name from a message content.
        /// </summary>
        /// <param name="messageContent">The content of the message to analyze.</param>
        /// <param name="nextAgentName">When this method returns, contains the next agent name if extraction succeeded.</param>
        /// <returns>True if extraction was successful; otherwise, false.</returns>
        private bool TryExtractNextAgentName(string messageContent, out string nextAgentName)
        {
            // Look for patterns like [NEXT:AgentName] or similar tags
            // This can be customized based on your orchestration needs
            
            nextAgentName = string.Empty;

            // Simple regex to extract agent names from [NEXT:AgentName] format
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
}