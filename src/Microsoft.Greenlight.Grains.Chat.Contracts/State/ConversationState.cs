using Microsoft.Greenlight.Shared.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.State;

[GenerateSerializer(GenerateFieldIds = GenerateFieldIds.PublicProperties)]
public class ConversationState
{
    public Guid Id { get; set; }
    public string? DocumentProcessName { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string? SystemPrompt { get; set; }
    /// <summary>
    /// The Provider Subject ID (OID/sub) of the user who started this conversation/orchestration.
    /// Standardized across orchestrations for per-user context propagation.
    /// </summary>
    public string? StartedByProviderSubjectId { get; set; }
    public List<Guid> ReferenceItemIds { get; set; } = new List<Guid>();
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    public List<ConversationSummary> Summaries { get; set; } = new List<ConversationSummary>();
}