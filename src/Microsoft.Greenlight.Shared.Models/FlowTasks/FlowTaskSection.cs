// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Represents a section within a Flow Task template that groups related requirements.
/// </summary>
public class FlowTaskSection : EntityBase
{
    /// <summary>
    /// Gets or sets the ID of the parent Flow Task template.
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

    // Navigation properties
    /// <summary>
    /// Gets or sets the parent Flow Task template.
    /// </summary>
    public virtual FlowTaskTemplate FlowTaskTemplate { get; set; } = null!;

    /// <summary>
    /// Gets or sets the requirements within this section.
    /// </summary>
    public virtual ICollection<FlowTaskRequirement> Requirements { get; set; } = new List<FlowTaskRequirement>();
}