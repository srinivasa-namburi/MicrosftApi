// Copyright (c) Microsoft Corporation. All rights reserved.

using System;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents an output template for generating results from a completed Flow Task.
/// </summary>
public class FlowTaskOutputTemplate : EntityBase
{
    /// <summary>
    /// Gets or sets the ID of the parent Flow Task template.
    /// </summary>
    public Guid FlowTaskTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the name of the output template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output type (e.g., McpTool, Document, Email).
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

    // Navigation properties
    /// <summary>
    /// Gets or sets the parent Flow Task template.
    /// </summary>
    public virtual FlowTaskTemplate FlowTaskTemplate { get; set; } = null!;
}