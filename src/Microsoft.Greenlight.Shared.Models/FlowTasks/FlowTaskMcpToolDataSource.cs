// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;
using Microsoft.Greenlight.Shared.Models.Plugins;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents a Flow Task data source that fetches data via MCP tool invocation.
/// </summary>
public class FlowTaskMcpToolDataSource : FlowTaskDataSource
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FlowTaskMcpToolDataSource"/> class.
    /// </summary>
    public FlowTaskMcpToolDataSource()
    {
        SourceType = nameof(FlowTaskMcpToolDataSource);
    }

    /// <summary>
    /// Gets or sets the ID of the MCP plugin that provides the tool.
    /// </summary>
    public Guid McpPluginId { get; set; }

    /// <summary>
    /// Gets or sets the name of the MCP tool to invoke.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional LLM prompt for transforming the tool response.
    /// </summary>
    public string? TransformPrompt { get; set; }

    // Navigation properties
    /// <summary>
    /// Gets or sets the MCP plugin that provides the tool.
    /// </summary>
    public virtual McpPlugin McpPlugin { get; set; } = null!;

    /// <summary>
    /// Gets or sets the parameters for the MCP tool invocation.
    /// </summary>
    public virtual ICollection<FlowTaskMcpToolParameter> Parameters { get; set; } = new List<FlowTaskMcpToolParameter>();
}