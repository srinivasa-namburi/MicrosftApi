// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

/// <summary>
/// Grain contract for aggregating system status information from various notifications.
/// This grain automatically receives status updates through the SignalR notification system
/// rather than requiring manual status reporting from individual grains.
/// </summary>
public interface ISystemStatusAggregatorGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Processes a status contribution from a notification.
    /// This is called automatically by the SignalRNotifierGrain for notifications that implement ISystemStatusNotification.
    /// </summary>
    Task ProcessStatusContributionAsync(SystemStatusContribution contribution);
    
    /// <summary>
    /// Gets the current comprehensive system status snapshot.
    /// </summary>
    Task<SystemStatusSnapshot> GetSystemStatusAsync();
    
    /// <summary>
    /// Gets the current status for a specific subsystem.
    /// </summary>
    Task<SubsystemStatus?> GetSubsystemStatusAsync(string source);
    
    /// <summary>
    /// Gets the current status for all items of a specific type.
    /// </summary>
    Task<List<ItemStatus>> GetItemStatusListAsync(string source);
    
    /// <summary>
    /// Clears expired status entries and performs cleanup.
    /// </summary>
    Task CleanupExpiredStatusAsync();
    
    /// <summary>
    /// Refreshes worker status from all concurrency coordinator grains.
    /// This provides real-time worker thread information beyond just SignalR notifications.
    /// </summary>
    Task RefreshWorkerStatusAsync();
}