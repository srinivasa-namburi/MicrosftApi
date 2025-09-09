// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Extensions.Logging;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Grains.ApiSpecific.Contracts;
using Microsoft.Greenlight.Shared.Contracts.Messages;
using Microsoft.Greenlight.Shared.Contracts.DTO.SystemStatus;
using Microsoft.Greenlight.Shared.Enums;
using Orleans;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.SystemStatus;

/// <summary>
/// Grain that aggregates system status information from various notifications automatically.
/// This grain receives status updates through the SignalR notification system rather than
/// requiring manual status reporting from individual grains.
/// </summary>
[Reentrant]
public class SystemStatusAggregatorGrain : Grain, ISystemStatusAggregatorGrain
{
    private readonly ILogger<SystemStatusAggregatorGrain> _logger;
    
    // In-memory state for fast access
    private readonly Dictionary<string, SubsystemStatus> _subsystemStatuses = new();
    private readonly Dictionary<string, SystemAlert> _activeAlerts = new();
    private DateTime _lastUpdatedUtc = DateTime.UtcNow;
    
    public SystemStatusAggregatorGrain(ILogger<SystemStatusAggregatorGrain> logger)
    {
        _logger = logger;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SystemStatusAggregatorGrain activated for key {GrainKey}", this.GetPrimaryKey());
        
        // Timer 1: cleanup expired entries every 2 minutes (was 5)
        this.RegisterGrainTimer<object>(CleanupExpiredStatusAsync, null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
        
        // Timer 2: refresh worker status more frequently for responsiveness (initial after 5s then every 30s)
        this.RegisterGrainTimer<object>(async _ => await RefreshWorkerStatusAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
        
        return base.OnActivateAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ProcessStatusContributionAsync(SystemStatusContribution contribution)
    {
        try
        {
            _logger.LogTrace("Processing status contribution: {Source}.{ItemKey} = {Status}", 
                contribution.Source, contribution.ItemKey, contribution.Status);

            // Get or create subsystem status
            if (!_subsystemStatuses.TryGetValue(contribution.Source, out var subsystemStatus))
            {
                subsystemStatus = new SubsystemStatus
                {
                    Source = contribution.Source,
                    DisplayName = GetDisplayNameForSource(contribution.Source),
                    OverallStatus = SystemHealthStatus.Unknown,
                    Items = new List<ItemStatus>(),
                    LastUpdatedUtc = DateTime.UtcNow
                };
                _subsystemStatuses[contribution.Source] = subsystemStatus;
            }

            // Update or add item status
            var existingItem = subsystemStatus.Items.FirstOrDefault(i => i.ItemKey == contribution.ItemKey);
            if (existingItem != null)
            {
                // Update existing item
                var updatedItem = existingItem with
                {
                    Status = contribution.Status,
                    Severity = contribution.Severity,
                    LastUpdatedUtc = DateTime.UtcNow,
                    StatusMessage = contribution.StatusMessage,
                    Properties = contribution.Properties,
                    ExpiresAtUtc = contribution.ExpiresAtUtc
                };
                
                var index = subsystemStatus.Items.IndexOf(existingItem);
                subsystemStatus.Items[index] = updatedItem;
            }
            else
            {
                // Add new item
                var newItem = new ItemStatus
                {
                    ItemKey = contribution.ItemKey,
                    Status = contribution.Status,
                    Severity = contribution.Severity,
                    LastUpdatedUtc = DateTime.UtcNow,
                    StatusMessage = contribution.StatusMessage,
                    Properties = contribution.Properties,
                    ExpiresAtUtc = contribution.ExpiresAtUtc
                };
                subsystemStatus.Items.Add(newItem);
            }

            // Update subsystem overall status and last updated time
            var updatedSubsystem = subsystemStatus with
            {
                OverallStatus = CalculateSubsystemHealth(subsystemStatus.Items),
                LastUpdatedUtc = DateTime.UtcNow
            };
            _subsystemStatuses[contribution.Source] = updatedSubsystem;

            // Handle alerts for critical issues
            HandleAlerts(contribution);

            _lastUpdatedUtc = DateTime.UtcNow;
            
            // Send real-time notification to all clients in the system-status group
            _ = NotifySystemStatusUpdateAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to send system status update notification");
                }
            });
            
            _logger.LogTrace("Status contribution processed successfully");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process status contribution from {Source}", contribution.Source);
            return Task.CompletedTask; // Don't fail the notification pipeline
        }
    }

    /// <inheritdoc />
    public async Task<SystemStatusSnapshot> GetSystemStatusAsync()
    {
        // Ensure we have current worker status - refresh if we don't have WorkerThreads subsystem or it's stale
        var workerSubsystem = _subsystemStatuses.GetValueOrDefault("WorkerThreads");
        var needsWorkerRefresh = workerSubsystem == null || 
                                 workerSubsystem.LastUpdatedUtc < DateTime.UtcNow.AddSeconds(-40) || // was 3 minutes
                                 !workerSubsystem.Items.Any();
        
        if (needsWorkerRefresh)
        {
            _logger.LogDebug("Worker status is missing or stale, refreshing...");
            await RefreshWorkerStatusAsync();
        }

        var snapshot = new SystemStatusSnapshot
        {
            LastUpdatedUtc = _lastUpdatedUtc,
            OverallStatus = CalculateOverallSystemHealth(),
            Subsystems = _subsystemStatuses.Values.ToList(),
            ActiveAlerts = _activeAlerts.Values.ToList()
        };

        return snapshot;
    }

    /// <inheritdoc />
    public Task<SubsystemStatus?> GetSubsystemStatusAsync(string source)
    {
        _subsystemStatuses.TryGetValue(source, out var subsystemStatus);
        return Task.FromResult(subsystemStatus);
    }

    /// <inheritdoc />
    public Task<List<ItemStatus>> GetItemStatusListAsync(string source)
    {
        if (_subsystemStatuses.TryGetValue(source, out var subsystemStatus))
        {
            return Task.FromResult(subsystemStatus.Items);
        }
        
        return Task.FromResult(new List<ItemStatus>());
    }

    /// <inheritdoc />
    public Task CleanupExpiredStatusAsync()
    {
        return CleanupExpiredStatusAsync(null);
    }

    /// <inheritdoc />
    public async Task RefreshWorkerStatusAsync()
    {
        try
        {
            _logger.LogDebug("Refreshing worker status from concurrency coordinators");
            
            // Get status from all concurrency coordinator categories
            var categories = Enum.GetValues<ConcurrencyCategory>();
            var tasks = categories.Select(async category =>
            {
                try
                {
                    var coordinator = GrainFactory.GetGrain<IGlobalConcurrencyCoordinatorGrain>(category.ToString());
                    var status = await coordinator.GetStatusAsync();
                    
                    // Create worker status contribution
                    var utilizationPercent = status.MaxConcurrency > 0 ? (double)status.ActiveWeight / status.MaxConcurrency * 100 : 0;
                    var contribution = new SystemStatusContribution
                    {
                        Source = "WorkerThreads",
                        ItemKey = $"{category}Workers",
                        Status = GetWorkerStatusString(status),
                        Severity = GetWorkerSeverity(status),
                        StatusType = SystemStatusType.WorkerStatus,
                        StatusMessage = $"Active: {status.ActiveWeight}/{status.MaxConcurrency}, Queued: {status.QueueLength}",
                        Properties = new Dictionary<string, string>
                        {
                            ["Category"] = category.ToString(),
                            ["MaxConcurrency"] = status.MaxConcurrency.ToString(),
                            ["ActiveWeight"] = status.ActiveWeight.ToString(),
                            ["QueueLength"] = status.QueueLength.ToString(),
                            ["UtilizationPercent"] = utilizationPercent.ToString("F1")
                        },
                        ExpiresAtUtc = DateTime.UtcNow.AddMinutes(10) // Refresh every 10 minutes
                    };
                    
                    await ProcessStatusContributionAsync(contribution);
                    return (category, (Exception?)null);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get status for {Category} workers", category);
                    return (category, ex);
                }
            });
            
            var results = await Task.WhenAll(tasks);
            var failedCategories = results.Where(r => r.Item2 != null).ToList();
            
            if (failedCategories.Any())
            {
                _logger.LogWarning("Failed to refresh status for {FailedCount}/{TotalCount} worker categories: {FailedCategories}", 
                    failedCategories.Count, categories.Length, string.Join(", ", failedCategories.Select(f => f.category)));
            }
            else
            {
                _logger.LogDebug("Successfully refreshed worker status for all {Count} categories", categories.Length);
                
                // Send notification after successful worker status refresh
                _ = NotifySystemStatusUpdateAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Failed to send worker status update notification");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh worker status");
        }
    }

    private Task CleanupExpiredStatusAsync(object? _)
    {
        try
        {
            var now = DateTime.UtcNow;
            var itemsRemoved = 0;
            var alertsRemoved = 0;

            // Clean up expired items in each subsystem
            foreach (var (source, subsystemStatus) in _subsystemStatuses.ToList())
            {
                var itemsToRemove = subsystemStatus.Items
                    .Where(item => item.ExpiresAtUtc.HasValue && item.ExpiresAtUtc.Value <= now)
                    .ToList();

                if (itemsToRemove.Any())
                {
                    foreach (var item in itemsToRemove)
                    {
                        subsystemStatus.Items.Remove(item);
                        itemsRemoved++;
                    }

                    // Update subsystem overall status
                    var updatedSubsystem = subsystemStatus with
                    {
                        OverallStatus = CalculateSubsystemHealth(subsystemStatus.Items),
                        LastUpdatedUtc = now
                    };
                    _subsystemStatuses[source] = updatedSubsystem;
                }

                // Remove empty subsystems
                if (!subsystemStatus.Items.Any())
                {
                    _subsystemStatuses.Remove(source);
                }
            }

            // Clean up expired alerts
            var expiredAlerts = _activeAlerts.Values
                .Where(alert => alert.CreatedUtc.AddHours(24) <= now) // Alerts expire after 24 hours
                .ToList();

            foreach (var alert in expiredAlerts)
            {
                _activeAlerts.Remove(alert.Id);
                alertsRemoved++;
            }

            if (itemsRemoved > 0 || alertsRemoved > 0)
            {
                _logger.LogDebug("Cleaned up {ItemsRemoved} expired status items and {AlertsRemoved} expired alerts", 
                    itemsRemoved, alertsRemoved);
                _lastUpdatedUtc = now;
                
                // Send notification after cleanup changes system status
                _ = NotifySystemStatusUpdateAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Failed to send cleanup status update notification");
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup expired status entries");
        }

        return Task.CompletedTask;
    }

    private SystemHealthStatus CalculateOverallSystemHealth()
    {
        if (!_subsystemStatuses.Any())
        {
            return SystemHealthStatus.Unknown;
        }

        var subsystemHealthStatuses = _subsystemStatuses.Values.Select(s => s.OverallStatus).ToList();

        if (subsystemHealthStatuses.Any(s => s == SystemHealthStatus.Critical))
        {
            return SystemHealthStatus.Critical;
        }

        if (subsystemHealthStatuses.Any(s => s == SystemHealthStatus.Warning))
        {
            return SystemHealthStatus.Warning;
        }

        if (subsystemHealthStatuses.All(s => s == SystemHealthStatus.Healthy))
        {
            return SystemHealthStatus.Healthy;
        }

        return SystemHealthStatus.Unknown;
    }

    private static SystemHealthStatus CalculateSubsystemHealth(List<ItemStatus> items)
    {
        if (!items.Any())
        {
            return SystemHealthStatus.Unknown;
        }

        if (items.Any(i => i.Severity == SystemStatusSeverity.Critical))
        {
            return SystemHealthStatus.Critical;
        }

        if (items.Any(i => i.Severity == SystemStatusSeverity.Warning))
        {
            return SystemHealthStatus.Warning;
        }

        return SystemHealthStatus.Healthy;
    }

    private void HandleAlerts(SystemStatusContribution contribution)
    {
        if (contribution.Severity == SystemStatusSeverity.Critical)
        {
            var alertId = $"{contribution.Source}:{contribution.ItemKey}:{contribution.StatusType}";
            
            // Create or update alert
            var alert = new SystemAlert
            {
                Id = alertId,
                Severity = contribution.Severity,
                Title = $"{GetDisplayNameForSource(contribution.Source)} Issue",
                Message = contribution.StatusMessage ?? $"{contribution.ItemKey} has status: {contribution.Status}",
                Source = contribution.Source,
                CreatedUtc = DateTime.UtcNow,
                Properties = contribution.Properties
            };

            _activeAlerts[alertId] = alert;
        }
        else if (contribution.StatusType == SystemStatusType.OperationCompleted && 
                 contribution.Severity == SystemStatusSeverity.Info)
        {
            // Clear related alerts when operations complete successfully
            var alertsToRemove = _activeAlerts.Values
                .Where(a => a.Source == contribution.Source && 
                           a.Properties.GetValueOrDefault("ItemKey") == contribution.ItemKey)
                .ToList();

            foreach (var alert in alertsToRemove)
            {
                _activeAlerts.Remove(alert.Id);
            }
        }
    }

    
    private static string GetDisplayNameForSource(string source)
    {
        return source switch
        {
            "VectorStore" => "Vector Store",
            "WorkerThreads" => "Worker Threads",
            "Ingestion" => "Document Ingestion",
            "Validation" => "Content Validation",
            "Generation" => "Document Generation",
            "Chat" => "Chat System",
            "Review" => "Review System",
            _ => source
        };
    }

    private static string GetWorkerStatusString(ConcurrencyStatus status)
    {
        if (status.MaxConcurrency == 0)
        {
            return "Disabled";
        }
        var utilizationPercent = (double)status.ActiveWeight / status.MaxConcurrency * 100;
        if (status.QueueLength == 0)
        {
            return utilizationPercent switch
            {
                0 => "Idle",
                < 50 => "Light Load",
                < 80 => "Moderate Load",
                < 95 => "High Load",
                _ => "Full Capacity"
            };
        }
        // Queue present but treat as normal operational state
        return utilizationPercent >= 95 ? "Queued (Saturated)" : "Queued";
    }
    
    private static SystemStatusSeverity GetWorkerSeverity(ConcurrencyStatus status)
    {
        if (status.MaxConcurrency == 0)
        {
            return SystemStatusSeverity.Warning;
        }
        var utilizationPercent = (double)status.ActiveWeight / status.MaxConcurrency * 100;
        var max = status.MaxConcurrency;
        var q = status.QueueLength;
        // Critical only for extremely large queues relative to capacity AND saturated
        if (q >= max * 5 && utilizationPercent >= 95)
        {
            return SystemStatusSeverity.Critical;
        }
        // Warning only if queue size significantly exceeds capacity or saturated with any queue
        if (q >= max * 2 || (utilizationPercent >= 95 && q > 0))
        {
            return SystemStatusSeverity.Warning;
        }
        return SystemStatusSeverity.Info;
    }

    private async Task NotifySystemStatusUpdateAsync()
    {
        try
        {
            var currentStatus = await GetSystemStatusAsync();
            var signalRNotifierGrain = GrainFactory.GetGrain<ISignalRNotifierGrain>(Guid.Empty);
            await signalRNotifierGrain.NotifySystemStatusUpdateAsync(currentStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send system status update notification");
        }
    }
}