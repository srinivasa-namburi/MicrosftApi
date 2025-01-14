using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001

namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation.Agentic
{
    /// <summary>
    /// A selection strategy that decides which agent to invoke next
    /// based on the content of the latest message or recipient stated in the message.
    /// </summary>
    public class ContentGenerationAgentSelectionStrategy : SelectionStrategy
    {
        private readonly ILogger<AgentAiCompletionService> _logger;

        public ContentGenerationAgentSelectionStrategy(
            ILogger<AgentAiCompletionService> logger)
        {
            _logger = logger;
        }

        protected async override Task<Agent> SelectAgentAsync(
            IReadOnlyList<Agent> agents, 
            IReadOnlyList<ChatMessageContent> history,
            CancellationToken cancellationToken = default)
        {
            if (!history.Any() || history.Count == 1)
            {
                _logger.LogInformation("Starting Agent Conversation with Writer Agent");
                return agents.Single(a => a.Name == "WriterAgent");
            }

            var lastMessage = history.LastOrDefault();

            if (lastMessage == null || lastMessage?.Content == null)
            {
                return agents.Single(a => a.Name == "WriterAgent");
            }

            _logger.LogInformation($"Received message from {lastMessage.AuthorName}:");
            _logger.LogInformation(lastMessage.Content);

            // Check if message explicitly targets an agent
            foreach (var agent in agents)
            {
                if (lastMessage.Content.StartsWith($"{agent.Name}:", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation($"Routing message to {agent.Name} since it was directly addressed as such");
                    return agent;
                }
            }

            // Fall back to tag-based routing if no explicit targeting
            if (lastMessage.Content.Contains("[KNOWLEDGE"))
            {
                _logger.LogInformation($"Received [KNOWLEDGE] information from KnowledgeRetrievelAgent");
                // Find the last agent that sent [REQUEST_KNOWLEDGE
                var lastRequestKnowledgeAgent = history
                    .LastOrDefault(m => m.Content != null && m.Content.Contains("[REQUEST_KNOWLEDGE"))
                    ?.AuthorName;

                if (lastRequestKnowledgeAgent == null)
                {
                    _logger.LogInformation("No previous agent requested knowledge, sending to ReviewerAgent");
                    return agents.Single(a => a.Name == "ReviewerAgent");
                }
                else
                {
                    _logger.LogInformation($"Routing message to {lastRequestKnowledgeAgent} since it requested knowledge last");
                    return agents.Single(a => a.Name == lastRequestKnowledgeAgent);
                }
            }
            
            if (lastMessage.Content.Contains("[ContentOutput"))
                return agents.Single(a => a.Name == "ReviewerAgent");

            if (lastMessage.Content.Contains("[REQUEST_KNOWLEDGE"))
            {
                _logger.LogInformation(
                    $"{lastMessage.AuthorName} requested knowledge - routing to KnowledgeRetrievalAgent");
                return agents.Single(a => a.Name == "KnowledgeRetrievalAgent");
            }

            if (lastMessage.Content.Contains("[REVISE"))
            {
                _logger.LogInformation(
                    $"Received request from {lastMessage.AuthorName} to revise certain content. Routing to WriterAgent");
                return agents.Single(a => a.Name == "WriterAgent");
            }

            if (lastMessage.Content.Contains("[CONTINUE"))
            {
                _logger.LogInformation(
                    $"Received request from {lastMessage.AuthorName} to continue expanding on content. Routing to WriterAgent");
                return agents.Single(a => a.Name == "WriterAgent");
            }


            if (lastMessage.Content.Contains("[REMOVE"))
            {
                _logger.LogInformation(
                    $"Received request from {lastMessage.AuthorName} to remove certain content. Routing to WriterAgent for removal");
                return agents.Single(a => a.Name == "WriterAgent");
            }

            // Default to ReviewerAgent if no clear routing
            return agents.Single(a => a.Name == "ReviewerAgent");
        }
    }
}