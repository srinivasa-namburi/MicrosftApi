// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task section information.
/// </summary>
public class FlowTaskSectionDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the section.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent template.
    /// </summary>
    public Guid FlowTaskTemplateId { get; set; }

    /// <summary>
    /// Gets or sets the name of the section.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name shown to users.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of this section's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the order in which this section appears.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets whether all requirements in this section must be collected.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets the prompt shown when collecting requirements from this section.
    /// </summary>
    public string? SectionPrompt { get; set; }

    /// <summary>
    /// Gets or sets the requirements within this section.
    /// </summary>
    public List<FlowTaskRequirementDto> Requirements { get; set; } = new();
}
