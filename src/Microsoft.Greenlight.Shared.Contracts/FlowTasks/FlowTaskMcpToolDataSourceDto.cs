// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task MCP tool data source information.
/// </summary>
public class FlowTaskMcpToolDataSourceDto : FlowTaskDataSourceDto
{
    /// <summary>
    /// Gets or sets the ID of the MCP plugin that provides the tool.
    /// </summary>
    public Guid? McpPluginId { get; set; }

    /// <summary>
    /// Gets or sets the name of the MCP tool to invoke.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional LLM prompt for transforming the tool response.
    /// </summary>
    public string? TransformPrompt { get; set; }

    /// <summary>
    /// Gets or sets the parameters for the MCP tool invocation.
    /// </summary>
    public List<FlowTaskMcpToolParameterDto> Parameters { get; set; } = new();
}
