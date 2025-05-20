// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
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
    /// Strategy for determining when to terminate a multi-agent conversation.
    /// Supports multiple termination conditions beyond just the [COMPLETE] tag.
    /// </summary>
    public class MultiAgentTerminationStrategy : TerminationStrategy
    {
        private readonly ILogger _logger;
        private readonly List<string> _terminationTags;
        private readonly HashSet<string> _agentsWithTerminationAuthority;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiAgentTerminationStrategy"/> class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic information.</param>
        /// <param name="terminationTags">List of tags that signal conversation termination.</param>
        /// <param name="agentsWithTerminationAuthority">Names of agents that have authority to terminate the conversation.</param>
        public MultiAgentTerminationStrategy(
            ILogger logger, 
            IEnumerable<string> terminationTags = null, 
            IEnumerable<string> agentsWithTerminationAuthority = null)
        {
            _logger = logger;
            _terminationTags = terminationTags?.ToList() ?? new List<string> { "[COMPLETE]" };
            _agentsWithTerminationAuthority = agentsWithTerminationAuthority != null ? 
                new HashSet<string>(agentsWithTerminationAuthority) : 
                new HashSet<string> { "ReviewerAgent", "OrchestratorAgent" };
        }

        /// <summary>
        /// Determines whether the agent conversation should be terminated.
        /// </summary>
        /// <param name="agent">The current agent.</param>
        /// <param name="history">Chat history so far.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the conversation should be terminated; otherwise, false.</returns>
        protected override Task<bool> ShouldAgentTerminateAsync(
            Agent agent, 
            IReadOnlyList<ChatMessageContent> history, 
            CancellationToken cancellationToken)
        {
            if (!history.Any())
            {
                return Task.FromResult(false);
            }

            var lastMessage = history.Last();
            
            // Only check for termination tags if the message is from an authorized agent
            if (_agentsWithTerminationAuthority.Contains(lastMessage.AuthorName))
            {
                foreach (var tag in _terminationTags)
                {
                    if (lastMessage.Content.Contains(tag, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Termination condition met: '{tag}' from agent '{lastMessage.AuthorName}'");
                        return Task.FromResult(true);
                    }
                }
            }

            // Check for maximum message count or other termination conditions
            // Add additional termination conditions as needed

            return Task.FromResult(false);
        }

        /// <summary>
        /// Adds a termination tag to the list of tags that can terminate the conversation.
        /// </summary>
        /// <param name="tag">The termination tag to add.</param>
        public void AddTerminationTag(string tag)
        {
            if (!string.IsNullOrWhiteSpace(tag) && !_terminationTags.Contains(tag))
            {
                _terminationTags.Add(tag);
            }
        }

        /// <summary>
        /// Adds an agent to the list of agents with authority to terminate the conversation.
        /// </summary>
        /// <param name="agentName">Name of the agent to grant termination authority.</param>
        public void AddAgentWithTerminationAuthority(string agentName)
        {
            if (!string.IsNullOrWhiteSpace(agentName))
            {
                _agentsWithTerminationAuthority.Add(agentName);
            }
        }
    }
}