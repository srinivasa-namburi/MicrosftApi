// Copyright (c) Microsoft Corporation. All rights reserved.

using Orleans;

namespace Microsoft.Greenlight.Grains.Shared.Scheduling;

/// <summary>
/// Scheduled grain interface for Flow Task template metadata reindexing.
/// </summary>
public interface IFlowTaskTemplateReindexingGrain : IGrainWithGuidKey
{
    /// <summary>
    /// Executes the Flow Task template metadata reindexing job.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync();
}
