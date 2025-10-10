// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents a Flow Task data source with static predefined values.
/// </summary>
public class FlowTaskStaticDataSource : FlowTaskDataSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskStaticDataSource"/> class.
    /// </summary>
    public FlowTaskStaticDataSource()
    {
        SourceType = nameof(FlowTaskStaticDataSource);
    }

    /// <summary>
    /// Gets or sets the static values as JSON array.
    /// </summary>
    public string ValuesJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the display format for the values.
    /// </summary>
    public string? DisplayFormat { get; set; }
}