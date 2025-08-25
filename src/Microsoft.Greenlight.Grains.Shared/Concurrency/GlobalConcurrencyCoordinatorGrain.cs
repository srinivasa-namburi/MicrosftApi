// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Greenlight.Shared.Configuration;
using Microsoft.Greenlight.Grains.Shared.Contracts;
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
        RegisterTimer(_ => CleanupExpiredLeasesAsync(), null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
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
                continue;
            }

            if (_activeWeight + next.Weight <= max)
            {
                _queue.Dequeue();
                var lease = GrantLease(next.RequesterId, next.Weight, next.LeaseTtl);
                next.Tcs.TrySetResult(lease);
                progressed = true;
            }
        }
    }

    private Task CleanupExpiredLeasesAsync()
    {
        if (_active.Count == 0) return Task.CompletedTask;

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
        }
        return Task.CompletedTask;
    }
}
