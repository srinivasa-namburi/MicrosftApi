using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;

#pragma warning disable SKEXP0110
#pragma warning disable SKEXP0001
namespace Microsoft.Greenlight.DocumentProcess.Shared.Generation.Agentic
{
    public class CompleteTagTerminationStrategy : TerminationStrategy
    {

        protected async override Task<bool> ShouldAgentTerminateAsync(Agent agent, IReadOnlyList<ChatMessageContent> history, CancellationToken cancellationToken)
        {
            var message = history.LastOrDefault();

            if (message.AuthorName == "ReviewerAgent" &&
                message.Content.Contains("[COMPLETE]", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}