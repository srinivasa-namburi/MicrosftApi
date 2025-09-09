// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Enums;
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

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

