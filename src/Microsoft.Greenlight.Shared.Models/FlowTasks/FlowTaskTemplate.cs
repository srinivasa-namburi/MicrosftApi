// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents a Flow Task template that defines a conversational workflow orchestration.
/// </summary>
public class FlowTaskTemplate : EntityBase
{
    /// <summary>
    /// Gets or sets the unique name of the Flow Task template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the template.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this Flow Task accomplishes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the Flow Task (e.g., DocumentGeneration, DataCollection).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the semantic trigger phrases that indicate this template should be used.
    /// </summary>
    public string[]? TriggerPhrases { get; set; }

    /// <summary>
    /// Gets or sets the initial prompt shown to users when the Flow Task starts.
    /// </summary>
    public string InitialPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the completion message shown when the Flow Task finishes successfully.
    /// </summary>
    public string? CompletionMessage { get; set; }

    /// <summary>
    /// Gets or sets whether this template is currently active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the version number of the template.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the metadata JSON for additional configuration.
    /// </summary>
    public string? MetadataJson { get; set; }

    // Navigation properties
    /// <summary>
    /// Gets or sets the sections that organize requirements in this template.
    /// </summary>
    public virtual ICollection<FlowTaskSection> Sections { get; set; } = new List<FlowTaskSection>();

    /// <summary>
    /// Gets or sets the output templates for generating results.
    /// </summary>
    public virtual ICollection<FlowTaskOutputTemplate> OutputTemplates { get; set; } = new List<FlowTaskOutputTemplate>();

    /// <summary>
    /// Gets or sets the data sources used by this template (includes MCP tool and static data sources).
    /// </summary>
    public virtual ICollection<FlowTaskDataSource> DataSources { get; set; } = new List<FlowTaskDataSource>();
}