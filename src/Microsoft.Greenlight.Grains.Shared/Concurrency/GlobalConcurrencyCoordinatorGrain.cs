// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Grains.Shared.Contracts;
using Microsoft.Greenlight.Shared.Enums;
using Orleans.Concurrency;

namespace Microsoft.Greenlight.Grains.Shared.Concurrency;

/// <summary>
/// A global concurrency coordinator implemented as a single activation per category (by string key).
/// Ensures cluster-wide concurrency limits using Orleans grain single-threaded execution and persistent state.
/// </summary>
[Reentrant]
public class GlobalConcurrencyCoordinatorGrain : Grain, IGlobalConcurrencyCoordinatorGrain
{
    private readonly ILogger<GlobalConcurrencyCoordinatorGrain> _logger;
    private readonly ServiceConfigurationOptions _config;

    private readonly Queue<PendingRequest> _queue = new();
    private readonly Dictionary<Guid, ActiveLease> _active = new();
    private int _activeWeight = 0;
    private ConcurrencyCategory _category;
    
    // For pushing status updates
    private ConcurrencyStatus? _lastPushedStatus;
    private DateTime _lastPushTime = DateTime.MinValue;

    // Throttling (lower to improve UI responsiveness)
    // Force push at least every 10s instead of 2 minutes.
    private static readonly TimeSpan ForcePushInterval = TimeSpan.FromSeconds(10);

    private sealed class PendingRequest
    {
        public string RequesterId { get; init; } = string.Empty;
        public int Weight { get; init; } = 1;
        public TimeSpan? LeaseTtl { get; init; }
        public TaskCompletionSource<ConcurrencyLease> Tcs { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public DateTime EnqueuedUtc { get; } = DateTime.UtcNow;
        public DateTime? DeadlineUtc { get; init; }
    }

    private sealed class ActiveLease
    {
        public ConcurrencyLease Lease { get; init; } = null!;
        public DateTime? ExpiresUtc => Lease.Ttl == null ? null : Lease.GrantedAtUtc + Lease.Ttl.Value;
    }

    public GlobalConcurrencyCoordinatorGrain(
        ILogger<GlobalConcurrencyCoordinatorGrain> logger,
        IOptions<ServiceConfigurationOptions> options)
    {
        _logger = logger;
        _config = options.Value;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        var key = this.GetPrimaryKeyString();
        _category = Enum.TryParse<ConcurrencyCategory>(key, ignoreCase: true, out var cat) ? cat : ConcurrencyCategory.Generation;

        // CRITICAL: Log reactivation to help diagnose state loss issues
        _logger.LogWarning("GlobalConcurrencyCoordinator for {Category} activated/reactivated. Active leases and queue state have been reset. Any existing leases are now orphaned.", _category);

        // Timer 1: lease cleanup
        RegisterTimer(_ => CleanupExpiredLeasesAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        // Timer 2: periodic heartbeat push so UI gets updates even when queue/active counts remain unchanged
        RegisterTimer(_ => PushStatusUpdateIfNeededAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ConcurrencyLease> AcquireAsync(string requesterId, int weight = 1, TimeSpan? waitTimeout = null, TimeSpan? leaseTtl = null)
    {
        if (weight <= 0) { weight = 1; }
        var max = GetMaxConcurrency();
        if (weight > max)
        {
            throw new InvalidOperationException($"Requested weight {weight} exceeds max {_category} concurrency {max}");
        }

        // CRITICAL: Detect potential state corruption after reactivation
        // If we have suspiciously low active count but requests are queueing, we may have lost state
        if (_queue.Count > max && _activeWeight == 0)
        {
            _logger.LogError("CRITICAL: Detected potential state loss in {Category} coordinator. Queue={Queue} but ActiveWeight=0. This indicates the coordinator was reactivated and lost track of active leases.",
                _category, _queue.Count);
        }

        // Fast-path if capacity is available and no queue
        var queueCount = _queue.Count;

        _logger.LogDebug("Checking fast-path for {Category}: queue={QueueCount}, activeWeight={ActiveWeight}, weight={Weight}, max={Max}, canGrant={CanGrant}",
            _category, queueCount, _activeWeight, weight, max, (queueCount == 0 && _activeWeight + weight <= max));

        if (queueCount == 0 && _activeWeight + weight <= max)
        {
            var lease = GrantLease(requesterId, weight, leaseTtl);
            _logger.LogInformation("Granted immediate lease {LeaseId} for {Category} to {Requester} (weight={Weight})", lease.LeaseId, _category, requesterId, weight);
            
            // Push status update for immediate lease grants
            _ = PushStatusUpdateIfNeededAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to push status update for immediate lease grant");
                }
            });
            
            return Task.FromResult(lease);
        }

        // Enqueue and await
        var deadlineUtc = waitTimeout.HasValue ? (DateTime?)DateTime.UtcNow.Add(waitTimeout.Value) : null;
        var pending = new PendingRequest { RequesterId = requesterId, Weight = weight, LeaseTtl = leaseTtl, DeadlineUtc = deadlineUtc };
        _queue.Enqueue(pending);
        _logger.LogDebug("Enqueued lease request for {Category} by {Requester} (weight={Weight}, activeWeight={ActiveWeight}, maxConcurrency={MaxConcurrency}, queue={Queue})",
            _category, requesterId, weight, _activeWeight, GetMaxConcurrency(), _queue.Count);

        // Try to drain the queue immediately in case capacity exists
        DrainQueue();

        // CRITICAL: Do NOT await the TaskCompletionSource here!
        // Awaiting would block the grain from processing other messages like GetStatusAsync
        // Even with [Reentrant], Orleans can't handle awaiting a TCS that needs grain re-entry to complete

        _logger.LogDebug("Request queued for {Category} requester {Requester}, will be processed when capacity available", _category, requesterId);

        // Return the task immediately - the caller will wait, but the grain remains free
        // DrainQueue (called by timer) will complete the task when capacity is available
        return pending.Tcs.Task;
    }

    public Task<bool> ReleaseAsync(Guid leaseId)
    {
        if (_active.Remove(leaseId, out var lease))
        {
            _activeWeight -= lease.Lease.Weight;
            if (_activeWeight < 0) _activeWeight = 0;
            _logger.LogDebug("Released lease {LeaseId} for {Category} (weight={Weight}); activeWeight={Active}", leaseId, _category, lease.Lease.Weight, _activeWeight);
            DrainQueue();
            
            // Push status update for lease releases
            _ = PushStatusUpdateIfNeededAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to push status update for lease release");
                }
            });
            
            return Task.FromResult(true);
        }

        _logger.LogWarning("Attempted to release unknown lease {LeaseId} for {Category}", leaseId, _category);
        return Task.FromResult(false);
    }

    public Task<ConcurrencyStatus> GetStatusAsync()
    {
        var status = new ConcurrencyStatus
        {
            MaxConcurrency = GetMaxConcurrency(),
            ActiveWeight = _activeWeight,
            QueueLength = _queue.Count
        };
        return Task.FromResult(status);
    }

    private int GetMaxConcurrency()
    {
        var max = _category switch
        {
            ConcurrencyCategory.Validation => _config.GreenlightServices.Scalability.NumberOfValidationWorkers,
            ConcurrencyCategory.Generation => _config.GreenlightServices.Scalability.NumberOfGenerationWorkers,
            ConcurrencyCategory.Ingestion => _config.GreenlightServices.Scalability.NumberOfIngestionWorkers,
            ConcurrencyCategory.Review => _config.GreenlightServices.Scalability.NumberOfReviewWorkers,
            ConcurrencyCategory.FlowChat => _config.GreenlightServices.Scalability.NumberOfFlowChatWorkers,
            _ => 1
        };

        // CRITICAL: Ensure we never return 0 which would block everything
        if (max <= 0)
        {
            _logger.LogError("CRITICAL: MaxConcurrency for {Category} is {Max} (<=0). Defaulting to 1 to prevent total blockage. Check configuration!", _category, max);
            max = 1;
        }

        // Log once per activation to help debug configuration issues
        if (_lastPushTime == DateTime.MinValue)
        {
            _logger.LogInformation("GlobalConcurrencyCoordinator for {Category} initialized with MaxConcurrency={Max}", _category, max);
        }

        return max;
    }

    private ConcurrencyLease GrantLease(string requesterId, int weight, TimeSpan? leaseTtl)
    {
        var lease = new ConcurrencyLease
        {
            LeaseId = Guid.NewGuid(),
            Category = _category,
            RequesterId = requesterId,
            Weight = weight,
            GrantedAtUtc = DateTime.UtcNow,
            Ttl = leaseTtl
        };
        _active[lease.LeaseId] = new ActiveLease { Lease = lease };
        _activeWeight += weight;
        return lease;
    }

    private void DrainQueue()
    {
        var max = GetMaxConcurrency();
        var progressed = true;
        var statusChanged = false;

        _logger.LogDebug("DrainQueue for {Category}: activeLeases={ActiveCount}, activeWeight={ActiveWeight}, max={Max}, queueLength={QueueLength}",
            _category, _active.Count, _activeWeight, max, _queue.Count);

        while (progressed)
        {
            progressed = false;
            if (_queue.Count == 0) break;

            var next = _queue.Peek();
            // Drop timed-out entries
            if (next.DeadlineUtc.HasValue && DateTime.UtcNow >= next.DeadlineUtc.Value)
            {
                _queue.Dequeue();
                next.Tcs.TrySetException(new TimeoutException("Timeout waiting for concurrency lease"));
                progressed = true;
                statusChanged = true; // Queue changed
                continue;
            }

            if (_activeWeight + next.Weight <= max)
            {
                _queue.Dequeue();
                var lease = GrantLease(next.RequesterId, next.Weight, next.LeaseTtl);
                next.Tcs.TrySetResult(lease);
                progressed = true;
                statusChanged = true; // Active weight and queue changed
                _logger.LogDebug("Granted queued lease {LeaseId} for {Category} to {Requester} (weight={Weight}, activeWeight={ActiveWeight}, queue={Queue})",
                    lease.LeaseId, _category, next.RequesterId, next.Weight, _activeWeight, _queue.Count);
            }
        }
        
        // Push status update if anything changed in the queue
        if (statusChanged)
        {
            _ = PushStatusUpdateIfNeededAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to push status update for queue changes");
                }
            });
        }
    }

    private async Task CleanupExpiredLeasesAsync()
    {
        if (_active.Count == 0) return;

        var now = DateTime.UtcNow;
        var expired = _active.Values.Where(a => a.ExpiresUtc.HasValue && a.ExpiresUtc.Value <= now).Select(a => a.Lease.LeaseId).ToList();
        foreach (var id in expired)
        {
            if (_active.Remove(id, out var lease))
            {
                _activeWeight -= lease.Lease.Weight;
                if (_activeWeight < 0) _activeWeight = 0;
                _logger.LogWarning("Reclaimed expired lease {LeaseId} for {Category} (weight={Weight})", id, _category, lease.Lease.Weight);
            }
        }

        if (expired.Count > 0)
        {
            DrainQueue();
            await PushStatusUpdateIfNeededAsync(); // Notify on changes
        }
    }

    /// <summary>
    /// Pushes status updates to the system status aggregator if there have been significant changes.
    /// Uses throttling to avoid excessive notifications while still being responsive.
    /// </summary>
    private async Task PushStatusUpdateIfNeededAsync(bool forceUpdate = false)
    {
        try
        {
            var now = DateTime.UtcNow;
            var currentStatus = new ConcurrencyStatus
            {
                MaxConcurrency = GetMaxConcurrency(),
                ActiveWeight = _activeWeight,
                QueueLength = _queue.Count
            };

            // Only push if there's a significant change or enough time has passed
            var shouldPush = forceUpdate ||
                            _lastPushedStatus == null ||
                            HasSignificantChange(_lastPushedStatus, currentStatus) ||
                            now - _lastPushTime > ForcePushInterval; // much shorter interval

            if (!shouldPush) return;

            // Create status contribution for the aggregator
            var utilizationPercent = currentStatus.MaxConcurrency > 0 ? (double)currentStatus.ActiveWeight / currentStatus.MaxConcurrency * 100 : 0;
            var contribution = new Microsoft.Greenlight.Shared.Contracts.Messages.SystemStatusContribution
            {
                Source = "WorkerThreads",
                ItemKey = $"{_category}Workers",
                Status = GetWorkerStatusString(currentStatus),
                Severity = GetWorkerSeverity(currentStatus),
                StatusType = Microsoft.Greenlight.Shared.Enums.SystemStatusType.WorkerStatus,
                StatusMessage = $"Active: {currentStatus.ActiveWeight}/{currentStatus.MaxConcurrency}, Queued: {currentStatus.QueueLength}",
                Properties = new Dictionary<string, string>
                {
                    ["Category"] = _category.ToString(),
                    ["MaxConcurrency"] = currentStatus.MaxConcurrency.ToString(),
                    ["ActiveWeight"] = currentStatus.ActiveWeight.ToString(),
                    ["QueueLength"] = currentStatus.QueueLength.ToString(),
                    ["UtilizationPercent"] = utilizationPercent.ToString("F1")
                },
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(5) // 5 minute expiry
            };

            // Push to the system status aggregator
            var aggregatorGrain = GrainFactory.GetGrain<ISystemStatusAggregatorGrain>(Guid.Empty);
            await aggregatorGrain.ProcessStatusContributionAsync(contribution);

            _lastPushedStatus = currentStatus;
            _lastPushTime = now;

            _logger.LogTrace("Pushed {Category} worker status update: Active={Active}/{Max}, Queue={Queue}", 
                _category, currentStatus.ActiveWeight, currentStatus.MaxConcurrency, currentStatus.QueueLength);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push status update for {Category} workers", _category);
        }
    }

    /// <summary>
    /// Determines if there's been a significant change in concurrency status that warrants a notification.
    /// </summary>
    private static bool HasSignificantChange(ConcurrencyStatus oldStatus, ConcurrencyStatus newStatus)
    {
        // Make more sensitive: any change in queue length or active weight triggers.
        return oldStatus.ActiveWeight != newStatus.ActiveWeight ||
               oldStatus.MaxConcurrency != newStatus.MaxConcurrency ||
               oldStatus.QueueLength != newStatus.QueueLength;
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

        // With queue present
        return utilizationPercent >= 95 ? "Queued (Saturated)" : "Queued";
    }
    
    private static Microsoft.Greenlight.Shared.Enums.SystemStatusSeverity GetWorkerSeverity(ConcurrencyStatus status)
    {
        // Updated severity mapping: Queues are normal unless they are proportionally large.
        if (status.MaxConcurrency == 0)
        {
            return Microsoft.Greenlight.Shared.Enums.SystemStatusSeverity.Warning;
        }

        var max = status.MaxConcurrency;
        var q = status.QueueLength;
        var utilizationPercent = (double)status.ActiveWeight / max * 100;

        // Critical only if queue is very large relative to capacity (e.g. > 5x) and fully saturated.
        if (q >= max * 5 && utilizationPercent >= 95)
        {
            return Microsoft.Greenlight.Shared.Enums.SystemStatusSeverity.Critical;
        }

        // Warning if queue exceeds capacity (>= 2x) OR saturated with any queue.
        if (q >= max * 2 || (utilizationPercent >= 95 && q > 0))
        {
            return Microsoft.Greenlight.Shared.Enums.SystemStatusSeverity.Warning;
        }

        // Otherwise informational.
        return Microsoft.Greenlight.Shared.Enums.SystemStatusSeverity.Info;
    }
}
