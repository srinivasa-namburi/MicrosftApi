// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for detailed Flow Task template information including all nested entities.
/// </summary>
public class FlowTaskTemplateDetailDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the template.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique name of the template.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the template.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the template.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the category of the template.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the trigger phrases for intent detection.
    /// </summary>
    public string[]? TriggerPhrases { get; set; }

    /// <summary>
    /// Gets or sets the initial prompt shown to users.
    /// </summary>
    public string InitialPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the completion message.
    /// </summary>
    public string? CompletionMessage { get; set; }

    /// <summary>
    /// Gets or sets whether the template is active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Gets or sets the version of the template.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the metadata JSON for additional configuration.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Gets or sets the sections that organize requirements.
    /// </summary>
    public List<FlowTaskSectionDto> Sections { get; set; } = new();

    /// <summary>
    /// Gets or sets the output templates for generating results.
    /// </summary>
    public List<FlowTaskOutputTemplateDto> OutputTemplates { get; set; } = new();

    /// <summary>
    /// Gets or sets the data sources used by this template.
    /// </summary>
    public List<FlowTaskDataSourceDto> DataSources { get; set; } = new();

    /// <summary>
    /// Gets or sets the created UTC timestamp.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the modified UTC timestamp.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
