// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;

namespace Microsoft.Greenlight.Grains.Chat.Contracts;

/// <summary>
/// Registry grain used to map backend conversation ids to one or more Flow session ids and vice versa.
/// Enables ChatMessageProcessorGrain to quickly discover which Flow sessions should receive
/// streaming updates for a backend conversation without scanning all Flow grains.
/// Backed by <see cref="Microsoft.Greenlight.Shared.Services.Caching.IAppCache"/> for multi-silo visibility.
/// </summary>
public interface IFlowBackendConversationRegistryGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Registers (or refreshes) a mapping between a backend conversation and a Flow session.
    /// </summary>
    /// <param name="backendConversationId">Backend conversation id.</param>
    /// <param name="flowSessionId">Flow session id.</param>
    /// <param name="processName">Document process short name.</param>
    Task RegisterAsync(Guid backendConversationId, Guid flowSessionId, string processName);

    /// <summary>
    /// Unregisters a mapping. Best-effort – silently returns if not present.
    /// </summary>
    Task UnregisterAsync(Guid backendConversationId, Guid flowSessionId);

    /// <summary>
    /// Returns Flow session ids watching the backend conversation.
    /// </summary>
    Task<List<Guid>> GetFlowSessionsAsync(Guid backendConversationId);

    /// <summary>
    /// Returns the map of process name to backend conversation id for a Flow session.
    /// </summary>
    Task<Dictionary<string, Guid>> GetBackendConversationsForFlowSessionAsync(Guid flowSessionId);

    /// <summary>
    /// Refresh TTL for a backend conversation mapping (sliding expiry).
    /// </summary>
    Task TouchAsync(Guid backendConversationId);
}
