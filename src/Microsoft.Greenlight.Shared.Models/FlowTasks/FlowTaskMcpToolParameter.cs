// Copyright (c) Microsoft Corporation. All rights reserved.

using System;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents a parameter for an MCP tool invocation in a Flow Task data source.
/// </summary>
public class FlowTaskMcpToolParameter : EntityBase
{
    /// <summary>
    /// Gets or sets the ID of the parent MCP tool data source.
    /// In TPH, this references the base type's ID.
    /// </summary>
    public Guid FlowTaskDataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parameter value (can be static or a template).
    /// </summary>
    public string ParameterValue { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this parameter value is a template that should be resolved.
    /// </summary>
    public bool IsTemplate { get; set; }

    /// <summary>
    /// Gets or sets the parameter data type for validation.
    /// </summary>
    public string? DataType { get; set; }

    // Navigation properties
    /// <summary>
    /// Gets or sets the parent MCP tool data source.
    /// </summary>
    public virtual FlowTaskMcpToolDataSource FlowTaskMcpToolDataSource { get; set; } = null!;
}