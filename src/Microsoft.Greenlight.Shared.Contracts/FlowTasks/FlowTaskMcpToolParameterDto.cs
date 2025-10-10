// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task MCP tool parameter information.
/// </summary>
public class FlowTaskMcpToolParameterDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the parameter.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent MCP tool data source.
    /// </summary>
    public Guid FlowTaskMcpToolDataSourceId { get; set; }

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
}
