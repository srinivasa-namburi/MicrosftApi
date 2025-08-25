// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

/// <summary>
/// Defines known concurrency categories that map to Greenlight Services scalability settings.
/// </summary>
public enum ConcurrencyCategory
{
    /// <summary>
    /// Document validation concurrency. Backed by NumberOfValidationWorkers.
    /// </summary>
    Validation = 0,

    /// <summary>
    /// Document generation concurrency. Backed by NumberOfGenerationWorkers.
    /// </summary>
    Generation = 1,

    /// <summary>
    /// Document ingestion concurrency. Backed by NumberOfIngestionWorkers.
    /// </summary>
    Ingestion = 2,

    /// <summary>
    /// Document review concurrency. Backed by NumberOfReviewWorkers.
    /// </summary>
    Review = 3
}

/// <summary>
/// Represents a granted concurrency lease.
/// </summary>
[GenerateSerializer]
public sealed class ConcurrencyLease
{
    /// <summary>
    /// The unique identifier of the lease.
    /// </summary>
    [Id(0)] public Guid LeaseId { get; set; }

    /// <summary>
    /// The category this lease belongs to.
    /// </summary>
    [Id(1)] public ConcurrencyCategory Category { get; set; }

    /// <summary>
    /// The requester identifier for diagnostics.
    /// </summary>
    [Id(2)] public string RequesterId { get; set; } = string.Empty;

    /// <summary>
    /// The weight (number of slots) consumed by this lease.
    /// </summary>
    [Id(3)] public int Weight { get; set; } = 1;

    /// <summary>
    /// When the lease was granted (UTC).
    /// </summary>
    [Id(4)] public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional TTL for the lease. If set and expired, the lease can be reclaimed.
    /// </summary>
    [Id(5)] public TimeSpan? Ttl { get; set; }
}

/// <summary>
/// Status information for a coordinator category.
/// </summary>
[GenerateSerializer]
public sealed class ConcurrencyStatus
{
    /// <summary>
    /// Maximum allowed concurrent weight for the category.
    /// </summary>
    [Id(0)] public int MaxConcurrency { get; set; }

    /// <summary>
    /// Current active weight (sum of active lease weights).
    /// </summary>
    [Id(1)] public int ActiveWeight { get; set; }

    /// <summary>
    /// Number of pending requests in the queue.
    /// </summary>
    [Id(2)] public int QueueLength { get; set; }
}

/// <summary>
/// Global concurrency coordinator grain contract. One activation per category (string primary key).
/// Key is the category name (Validation/Generation/Ingestion/Review).
/// </summary>
public interface IGlobalConcurrencyCoordinatorGrain : IGrainWithStringKey
{
    /// <summary>
    /// Attempts to acquire a lease for the specified requester. Will await until granted or timeout.
    /// </summary>
    /// <param name="requesterId">Requester identifier (for diagnostics).</param>
    /// <param name="weight">Number of slots to acquire. Default 1.</param>
    /// <param name="waitTimeout">Optional wait timeout; if exceeded, the call fails with TimeoutException.</param>
    /// <param name="leaseTtl">Optional TTL for the lease; if provided, lease may be reclaimed after TTL expires.</param>
    /// <returns>A granted <see cref="ConcurrencyLease"/>.</returns>
    [ResponseTimeout("2.00:00:00")] // Allow up to 2 days to match long waitTimeout callers
    Task<ConcurrencyLease> AcquireAsync(string requesterId, int weight = 1, TimeSpan? waitTimeout = null, TimeSpan? leaseTtl = null);

    /// <summary>
    /// Releases a previously granted lease.
    /// </summary>
    /// <param name="leaseId">The lease id to release.</param>
    /// <returns>True if released, false otherwise.</returns>
    Task<bool> ReleaseAsync(Guid leaseId);

    /// <summary>
    /// Gets current status for the coordinator.
    /// </summary>
    Task<ConcurrencyStatus> GetStatusAsync();
}
