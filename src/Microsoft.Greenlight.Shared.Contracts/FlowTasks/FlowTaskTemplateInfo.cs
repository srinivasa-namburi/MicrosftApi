// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task template information used across boundaries.
/// </summary>
public class FlowTaskTemplateInfo
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
    /// Gets or sets the number of sections in the template.
    /// </summary>
    public int SectionCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of requirements across all sections.
    /// </summary>
    public int TotalRequirementCount { get; set; }
}
