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
        // Timer 1: lease cleanup
        RegisterTimer(_ => CleanupExpiredLeasesAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        // Timer 2: periodic heartbeat push so UI gets updates even when queue/active counts remain unchanged
        RegisterTimer(_ => PushStatusUpdateIfNeededAsync(), null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<ConcurrencyLease> AcquireAsync(string requesterId, int weight = 1, TimeSpan? waitTimeout = null, TimeSpan? leaseTtl = null)
    {
        if (weight <= 0) { weight = 1; }
        var max = GetMaxConcurrency();
        if (weight > max)
        {
            throw new InvalidOperationException($"Requested weight {weight} exceeds max {_category} concurrency {max}");
        }

        // Fast-path if capacity is available and no queue
        if (_queue.Count == 0 && _activeWeight + weight <= max)
        {
            var lease = GrantLease(requesterId, weight, leaseTtl);
            _logger.LogDebug("Granted immediate lease {LeaseId} for {Category} to {Requester} (weight={Weight})", lease.LeaseId, _category, requesterId, weight);
            
            // Push status update for immediate lease grants
            _ = PushStatusUpdateIfNeededAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to push status update for immediate lease grant");
                }
            });
            
            return lease;
        }

        // Enqueue and await
        var deadlineUtc = waitTimeout.HasValue ? (DateTime?)DateTime.UtcNow.Add(waitTimeout.Value) : null;
        var pending = new PendingRequest { RequesterId = requesterId, Weight = weight, LeaseTtl = leaseTtl, DeadlineUtc = deadlineUtc };
        _queue.Enqueue(pending);
        _logger.LogDebug("Enqueued lease request for {Category} by {Requester} (weight={Weight}, queue={Queue})", _category, requesterId, weight, _queue.Count);

        // Try to drain the queue immediately in case capacity exists
        DrainQueue();

        using var cts = waitTimeout.HasValue ? new CancellationTokenSource(waitTimeout.Value) : null;
        using (cts)
        {
            if (cts != null)
            {
                await using var reg = cts.Token.UnsafeRegister(state =>
                {
                    var t = (TaskCompletionSource<ConcurrencyLease>)state!;
                    t.TrySetException(new TimeoutException("Timeout waiting for concurrency lease"));
                }, pending.Tcs);
            }

            return await pending.Tcs.Task.ConfigureAwait(false);
        }
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
        return _category switch
        {
            ConcurrencyCategory.Validation => _config.GreenlightServices.Scalability.NumberOfValidationWorkers,
            ConcurrencyCategory.Generation => _config.GreenlightServices.Scalability.NumberOfGenerationWorkers,
            ConcurrencyCategory.Ingestion => _config.GreenlightServices.Scalability.NumberOfIngestionWorkers,
            ConcurrencyCategory.Review => _config.GreenlightServices.Scalability.NumberOfReviewWorkers,
            _ => 1
        };
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
