// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task output template information.
/// </summary>
public class FlowTaskOutputTemplateDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the output template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent template.
    /// </summary>
    public Guid FlowTaskTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the name of the output template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output type (e.g., McpTool, TextSummary, DocumentGeneration, Link).
    /// </summary>
    public string OutputType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the template content (can contain placeholders).
    /// </summary>
    public string TemplateContent { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type (e.g., text/plain, text/html, application/json).
    /// </summary>
    public string ContentType { get; set; } = "text/plain";

    /// <summary>
    /// Gets or sets the MCP plugin ID if this output invokes an MCP tool.
    /// </summary>
    public Guid? McpPluginId { get; set; }

    /// <summary>
    /// Gets or sets the MCP tool name if this output invokes an MCP tool.
    /// </summary>
    public string? McpToolName { get; set; }

    /// <summary>
    /// Gets or sets the order in which this output should be executed.
    /// </summary>
    public int ExecutionOrder { get; set; }

    /// <summary>
    /// Gets or sets whether this output is required for task completion.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets the transformation rules as JSON for processing the output.
    /// </summary>
    public string? TransformationRulesJson { get; set; }
}
