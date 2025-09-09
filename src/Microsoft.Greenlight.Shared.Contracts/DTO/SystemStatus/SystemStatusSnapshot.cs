// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Greenlight.Shared.Enums;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;

/// <summary>
/// Comprehensive snapshot of system status aggregated from notifications.
/// </summary>
public record SystemStatusSnapshot
{
    public required DateTime LastUpdatedUtc { get; init; }
    public required SystemHealthStatus OverallStatus { get; init; }
    public required List<SubsystemStatus> Subsystems { get; init; }
    public required List<SystemAlert> ActiveAlerts { get; init; }
    public string? StatusMessage { get; init; }
    
    /// <summary>
    /// Quick summary statistics across all subsystems.
    /// </summary>
    public SystemStatusSummary Summary => CalculateSummary();
    
    private SystemStatusSummary CalculateSummary()
    {
        var totalItems = Subsystems.Sum(s => s.Items.Count);
        var healthyItems = Subsystems.Sum(s => s.Items.Count(i => i.Severity == SystemStatusSeverity.Info));
        var warningItems = Subsystems.Sum(s => s.Items.Count(i => i.Severity == SystemStatusSeverity.Warning));
        var criticalItems = Subsystems.Sum(s => s.Items.Count(i => i.Severity == SystemStatusSeverity.Critical));
        
        return new SystemStatusSummary
        {
            TotalSubsystems = Subsystems.Count,
            HealthySubsystems = Subsystems.Count(s => s.OverallStatus == SystemHealthStatus.Healthy),
            WarningSubsystems = Subsystems.Count(s => s.OverallStatus == SystemHealthStatus.Warning),
            CriticalSubsystems = Subsystems.Count(s => s.OverallStatus == SystemHealthStatus.Critical),
            TotalItems = totalItems,
            HealthyItems = healthyItems,
            WarningItems = warningItems,
            CriticalItems = criticalItems,
            ActiveAlerts = ActiveAlerts.Count,
            CriticalAlerts = ActiveAlerts.Count(a => a.Severity == SystemStatusSeverity.Critical),
            WarningAlerts = ActiveAlerts.Count(a => a.Severity == SystemStatusSeverity.Warning)
        };
    }
}

/// <summary>
/// Status information for a subsystem (e.g., VectorStore, WorkerThreads, Ingestion).
/// </summary>
public record SubsystemStatus
{
    public required string Source { get; init; }
    public required string DisplayName { get; init; }
    public required SystemHealthStatus OverallStatus { get; init; }
    public required List<ItemStatus> Items { get; init; }
    public required DateTime LastUpdatedUtc { get; init; }
    public string? StatusMessage { get; init; }
    
    /// <summary>
    /// Gets counts by status type for this subsystem.
    /// </summary>
    public Dictionary<string, int> StatusCounts => Items
        .GroupBy(i => i.Status)
        .ToDictionary(g => g.Key, g => g.Count());
}

/// <summary>
/// Status information for an individual item within a subsystem.
/// </summary>
public record ItemStatus
{
    public required string ItemKey { get; init; }
    public required string Status { get; init; }
    public required SystemStatusSeverity Severity { get; init; }
    public required DateTime LastUpdatedUtc { get; init; }
    public string? StatusMessage { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
    public DateTime? ExpiresAtUtc { get; init; }
}

/// <summary>
/// Quick summary statistics for the entire system.
/// </summary>
public record SystemStatusSummary
{
    public int TotalSubsystems { get; init; }
    public int HealthySubsystems { get; init; }
    public int WarningSubsystems { get; init; }
    public int CriticalSubsystems { get; init; }
    public int TotalItems { get; init; }
    public int HealthyItems { get; init; }
    public int WarningItems { get; init; }
    public int CriticalItems { get; init; }
    public int ActiveAlerts { get; init; }
    public int CriticalAlerts { get; init; }
    public int WarningAlerts { get; init; }
}

/// <summary>
/// System alert derived from status contributions.
/// </summary>
public record SystemAlert
{
    public required string Id { get; init; }
    public required SystemStatusSeverity Severity { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string Source { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}