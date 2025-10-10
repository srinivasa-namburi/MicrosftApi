// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task static data source information.
/// </summary>
public class FlowTaskStaticDataSourceDto : FlowTaskDataSourceDto
{
    /// <summary>
    /// Gets or sets the static values as JSON array.
    /// </summary>
    public string ValuesJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the display format for the values.
    /// </summary>
    public string? DisplayFormat { get; set; }
}
