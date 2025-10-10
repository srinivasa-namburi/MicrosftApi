// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Greenlight.Shared.Contracts.FlowTasks;

namespace Microsoft.Greenlight.Grains.Chat.Contracts.State;

/// <summary>
/// State for FlowTaskStateGrain. Stores structured requirement values during agentic Flow Task execution.
/// </summary>
public class FlowTaskStateGrainState
{
    /// <summary>
    /// Gets or sets the collected requirement values (fieldName → value).
    /// </summary>
    public Dictionary<string, object?> CollectedValues { get; set; } = new();

    /// <summary>
    /// Gets or sets the set of field names that have been revised by the user.
    /// </summary>
    public HashSet<string> RevisedFields { get; set; } = new();

    /// <summary>
    /// Gets or sets the current section being processed.
    /// </summary>
    public string CurrentSection { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Flow Task template associated with this execution.
    /// </summary>
    public FlowTaskTemplateDetailDto? Template { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last update to this state.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
