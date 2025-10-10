// Copyright (c) Microsoft Corporation. All rights reserved.

using System;
using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// Provides a summary of a Flow Task execution.
/// </summary>
public class FlowTaskExecutionSummary
{
    /// <summary>
    /// Gets or sets the execution ID.
    /// </summary>
    public Guid ExecutionId { get; set; }

    /// <summary>
    /// Gets or sets the template ID.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template name.
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the execution completed.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the current state.
    /// </summary>
    public FlowTaskExecutionState State { get; set; }

    /// <summary>
    /// Gets or sets the total number of requirements.
    /// </summary>
    public int TotalRequirements { get; set; }

    /// <summary>
    /// Gets or sets the number of collected requirements.
    /// </summary>
    public int CollectedRequirements { get; set; }

    /// <summary>
    /// Gets or sets the number of validated requirements.
    /// </summary>
    public int ValidatedRequirements { get; set; }

    /// <summary>
    /// Gets or sets the sections with their completion status.
    /// </summary>
    public List<FlowTaskSectionSummary> Sections { get; set; } = new List<FlowTaskSectionSummary>();

    /// <summary>
    /// Gets or sets the collected requirement values.
    /// </summary>
    public Dictionary<string, object?> CollectedValues { get; set; } = new Dictionary<string, object?>();

    /// <summary>
    /// Gets or sets any validation issues.
    /// </summary>
    public List<string> ValidationIssues { get; set; } = new List<string>();
}

/// <summary>
/// Provides a summary of a Flow Task section.
/// </summary>
public class FlowTaskSectionSummary
{
    /// <summary>
    /// Gets or sets the section name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the section display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the section is complete.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// Gets or sets the number of requirements in the section.
    /// </summary>
    public int RequirementCount { get; set; }

    /// <summary>
    /// Gets or sets the number of collected requirements.
    /// </summary>
    public int CollectedCount { get; set; }
}