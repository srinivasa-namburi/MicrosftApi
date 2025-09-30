// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Chat.Contracts;
using Microsoft.Greenlight.Shared.Services.Caching;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Chat;

/// <summary>
/// Implementation of <see cref="IFlowBackendConversationRegistryGrain"/>.
/// Maintains bidirectional mappings using cache for cross-silo visibility.
/// Keys:
/// flow:reg:bk:{backendId} => serialized RegistryEntry (list of flow sessions, processName, lastSeen)
/// flow:reg:fs:{flowSessionId} => serialized FlowSessionIndex (processName -> backendId)
/// TTL sliding (default 30 minutes) refreshed on Register/Touch.
/// </summary>
[Reentrant]
public class FlowBackendConversationRegistryGrain : Grain, IFlowBackendConversationRegistryGrain
{
    private readonly IAppCache _cache;
    private readonly ILogger<FlowBackendConversationRegistryGrain> _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(30);

    private record RegistryEntry(string ProcessName, HashSet<Guid> FlowSessions, DateTime LastSeenUtc);
    private record FlowSessionIndex(Dictionary<string, Guid> ProcessToBackend, DateTime LastSeenUtc);

    public FlowBackendConversationRegistryGrain(IAppCache cache, ILogger<FlowBackendConversationRegistryGrain> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task RegisterAsync(Guid backendConversationId, Guid flowSessionId, string processName)
    {
        if (backendConversationId == Guid.Empty || flowSessionId == Guid.Empty || string.IsNullOrWhiteSpace(processName))
        {
            return;
        }
        var bkKey = BuildBackendKey(backendConversationId);
        var fsKey = BuildFlowSessionKey(flowSessionId);

        var entry = await _cache.GetOrCreateAsync(bkKey, _ => Task.FromResult(new RegistryEntry(processName, new HashSet<Guid>(), DateTime.UtcNow)), Ttl, allowDistributed: true);
        if (entry.ProcessName != processName)
        {
            // If process changed (rare), overwrite
            entry = entry with { ProcessName = processName };
        }
        entry.FlowSessions.Add(flowSessionId);
        entry = entry with { LastSeenUtc = DateTime.UtcNow };
        await _cache.SetAsync(bkKey, entry, Ttl, allowDistributed: true);

        var fsIndex = await _cache.GetOrCreateAsync(fsKey, _ => Task.FromResult(new FlowSessionIndex(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase), DateTime.UtcNow)), Ttl, allowDistributed: true);
        fsIndex.ProcessToBackend[processName] = backendConversationId;
        fsIndex = fsIndex with { LastSeenUtc = DateTime.UtcNow };
        await _cache.SetAsync(fsKey, fsIndex, Ttl, allowDistributed: true);

        _logger.LogDebug("Registered backend {BackendId} -> flow {FlowSessionId} ({Process})", backendConversationId, flowSessionId, processName);
    }

   public async Task UnregisterAsync(Guid backendConversationId, Guid flowSessionId)
    {
        if (backendConversationId == Guid.Empty || flowSessionId == Guid.Empty)
        {
            return;
        }
        var bkKey = BuildBackendKey(backendConversationId);
        var entry = await _cache.GetOrCreateAsync<RegistryEntry>(bkKey, _ => Task.FromResult<RegistryEntry>(null!), Ttl, allowDistributed: true);
        if (entry != null && entry.FlowSessions.Remove(flowSessionId))
        {
            if (entry.FlowSessions.Count == 0)
            {
                await _cache.RemoveAsync(bkKey);
            }
            else
            {
                await _cache.SetAsync(bkKey, entry with { LastSeenUtc = DateTime.UtcNow }, Ttl, allowDistributed: true);
            }
        }

        // Remove from flow session index
        var fsKey = BuildFlowSessionKey(flowSessionId);
        var fsIndex = await _cache.GetOrCreateAsync<FlowSessionIndex>(fsKey, _ => Task.FromResult<FlowSessionIndex>(null!), Ttl, allowDistributed: true);
        if (fsIndex != null)
        {
            var removed = fsIndex.ProcessToBackend.Where(kvp => kvp.Value == backendConversationId).Select(k => k.Key).ToList();
            foreach (var key in removed)
            {
                fsIndex.ProcessToBackend.Remove(key);
            }
            if (fsIndex.ProcessToBackend.Count == 0)
            {
                await _cache.RemoveAsync(fsKey);
            }
            else
            {
                await _cache.SetAsync(fsKey, fsIndex with { LastSeenUtc = DateTime.UtcNow }, Ttl, allowDistributed: true);
            }
        }
        _logger.LogDebug("Unregistered backend {BackendId} for flow {FlowSessionId}", backendConversationId, flowSessionId);
    }

    public async Task<List<Guid>> GetFlowSessionsAsync(Guid backendConversationId)
    {
        if (backendConversationId == Guid.Empty)
        {
            return new List<Guid>();
        }
        var bkKey = BuildBackendKey(backendConversationId);
        var entry = await _cache.GetOrCreateAsync<RegistryEntry>(bkKey, _ => Task.FromResult<RegistryEntry>(null!), Ttl, allowDistributed: true);
        return entry?.FlowSessions.ToList() ?? new List<Guid>();
    }

    public async Task<Dictionary<string, Guid>> GetBackendConversationsForFlowSessionAsync(Guid flowSessionId)
    {
        if (flowSessionId == Guid.Empty)
        {
            return new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        }
        var fsKey = BuildFlowSessionKey(flowSessionId);
        var fsIndex = await _cache.GetOrCreateAsync<FlowSessionIndex>(fsKey, _ => Task.FromResult<FlowSessionIndex>(null!), Ttl, allowDistributed: true);
        return fsIndex?.ProcessToBackend ?? new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task TouchAsync(Guid backendConversationId)
    {
        var bkKey = BuildBackendKey(backendConversationId);
        var entry = await _cache.GetOrCreateAsync<RegistryEntry>(bkKey, _ => Task.FromResult<RegistryEntry>(null!), Ttl, allowDistributed: true);
        if (entry != null)
        {
            await _cache.SetAsync(bkKey, entry with { LastSeenUtc = DateTime.UtcNow }, Ttl, allowDistributed: true);
        }
    }

    private static string BuildBackendKey(Guid backendId) => $"flow:reg:bk:{backendId}";
    private static string BuildFlowSessionKey(Guid flowSessionId) => $"flow:reg:fs:{flowSessionId}";
}
