using Microsoft.Greenlight.Grains.Shared.Contracts.State;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

/// <summary>
/// Orchestrates reindexing of content references into SK vector store per ContentReferenceType.
/// </summary>
public interface IContentReferenceReindexOrchestrationGrain : IGrainWithStringKey
{
    Task StartReindexingAsync(ContentReferenceType type, string reason);
    Task<ContentReferenceReindexState> GetStateAsync();
    Task<bool> IsRunningAsync();
}

