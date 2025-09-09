// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

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

