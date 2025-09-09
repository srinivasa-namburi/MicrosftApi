// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.Messages;

/// <summary>
/// Interface for notifications that can contribute to system status monitoring.
/// Implementing this interface allows notifications to be automatically picked up 
/// by the system status aggregator without requiring manual status updates.
/// </summary>
public interface ISystemStatusNotification
{
    /// <summary>
    /// Gets the system status information that this notification contributes.
    /// </summary>
    SystemStatusContribution GetStatusContribution();
}

/// <summary>
/// Information that a notification contributes to overall system status.
/// </summary>
public class SystemStatusContribution
{
    /// <summary>
    /// The subsystem or component this notification relates to (e.g., "VectorStore", "WorkerThreads", "Ingestion").
    /// </summary>
    public required string Source { get; init; }
    
    /// <summary>
    /// The type of status update this represents.
    /// </summary>
    public required SystemStatusType StatusType { get; init; }
    
    /// <summary>
    /// Key identifier for tracking this particular item/operation (e.g., index name, worker thread ID, orchestration ID).
    /// </summary>
    public required string ItemKey { get; init; }
    
    /// <summary>
    /// Current status of the item/operation.
    /// </summary>
    public required string Status { get; init; }
    
    /// <summary>
    /// Human-readable message about the current status.
    /// </summary>
    public string? StatusMessage { get; init; }
    
    /// <summary>
    /// Severity level of this status update.
    /// </summary>
    public SystemStatusSeverity Severity { get; init; } = SystemStatusSeverity.Info;
    
    /// <summary>
    /// Additional metadata about this status contribution.
    /// </summary>
    public Dictionary<string, string> Properties { get; init; } = new();
    
    /// <summary>
    /// Optional expiry time for this status contribution. If null, the status persists until updated.
    /// </summary>
    public DateTime? ExpiresAtUtc { get; init; }
}