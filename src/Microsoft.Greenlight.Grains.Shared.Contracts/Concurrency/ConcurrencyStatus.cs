// Copyright (c) Microsoft Corporation. All rights reserved.
using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Contracts;

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

