using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;

namespace Microsoft.Greenlight.Web.Shared.ServiceClients;

/// <summary>
/// API client for system status monitoring operations.
/// </summary>
public interface ISystemStatusApiClient : IServiceClient
{
    /// <summary>
    /// Gets the current comprehensive system status snapshot.
    /// </summary>
    Task<SystemStatusSnapshot> GetSystemStatusAsync();
    
    /// <summary>
    /// Gets the current status for a specific subsystem.
    /// </summary>
    /// <param name="source">The subsystem source (e.g., "VectorStore", "WorkerThreads", "Ingestion")</param>
    Task<SubsystemStatus?> GetSubsystemStatusAsync(string source);
}