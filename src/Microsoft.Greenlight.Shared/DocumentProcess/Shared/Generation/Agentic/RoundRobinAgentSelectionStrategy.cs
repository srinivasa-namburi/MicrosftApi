using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

namespace Microsoft.Greenlight.Shared.DocumentProcess.Shared.Generation.Agentic
{
#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

    /// <summary>
    /// Example: A simpler selection strategy for exactly two agents:
    /// If the last message was from ContentAgent, route next to ReviewerAgent, else route to ContentAgent.
    /// This is just an example—adjust logic as needed.
    /// </summary>
    public class RoundRobinAgentSelectionStrategy : SelectionStrategy
    {
        private readonly ILogger<AgentAiCompletionService> _logger;

        public RoundRobinAgentSelectionStrategy(ILogger<AgentAiCompletionService> logger)
        {
            _logger = logger;
        }

        protected override Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents,
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (!agents.Any()) throw new InvalidOperationException("No agents available.");

            var contentAgent = agents.Single(a => a.Name == "ContentAgent");
            var reviewerAgent = agents.Single(a => a.Name == "ReviewerAgent");

            if (history.Count == 0)
            {
                _logger.LogInformation("No conversation yet—starting with ContentAgent.");
                return Task.FromResult<Agent>(contentAgent);
            }

            var lastMsg = history.Last();
            _logger.LogInformation($"Last message from {lastMsg.AuthorName}");

            // If last from ContentAgent, route to ReviewerAgent, else route to ContentAgent
            if (lastMsg.AuthorName == "ContentAgent")
            {
                _logger.LogInformation("Routing to ReviewerAgent.");
                return Task.FromResult<Agent>(reviewerAgent);
            }
            else
            {
                _logger.LogInformation("Routing to ContentAgent.");
                return Task.FromResult<Agent>(contentAgent);
            }
        }
    }
}