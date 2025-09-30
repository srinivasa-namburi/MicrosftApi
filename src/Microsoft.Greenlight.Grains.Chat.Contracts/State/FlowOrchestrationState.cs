using Microsoft.Greenlight.Shared.Models;
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.State
{
    /// <summary>
    /// Persistent state for Flow orchestration - now includes its own conversation management.
    /// </summary>
    [GenerateSerializer]
    public record FlowOrchestrationState
    {
        public Guid SessionId { get; init; }
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime LastActivityUtc { get; init; } = DateTime.UtcNow;
        public string? UserOid { get; init; }
        public string? UserName { get; init; }

        // User-facing conversation state (what user sees)
        public List<ChatMessage> UserConversationMessages { get; init; } = new();
        public List<ConversationSummary> UserConversationSummaries { get; init; } = new();
        public string? SystemPrompt { get; init; }

        // Backend orchestration state (internal)
        public List<Guid> ActiveBackendConversationIds { get; init; } = new();
        public List<string> EngagedDocumentProcesses { get; init; } = new();
        public int QueryCount { get; init; }

        // Tool orchestration state
        public HashSet<string> AvailablePlugins { get; init; } = new();
        public Dictionary<string, DateTime> PluginLastUsed { get; init; } = new();
        public List<string> ActiveCapabilities { get; init; } = new();

        // Orchestration metadata
        public Dictionary<string, object> OrchestrationMetadata { get; init; } = new();

        // Message superseding tracking (for Flow aggregation messages)
        // Key is the superseded message ID, value is the superseding message ID
        public Dictionary<Guid, Guid> SupersededMessages { get; init; } = new();
    }
}