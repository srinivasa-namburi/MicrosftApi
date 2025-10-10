// Copyright (c) Microsoft Corporation. All rights reserved.

using System;

namespace Microsoft.Greenlight.Shared.Models.FlowTasks;

/// <summary>
/// Base class for Flow Task data sources that provide dynamic values for requirements.
/// Uses TPH (Table Per Hierarchy) inheritance pattern.
/// </summary>
public abstract class FlowTaskDataSource : EntityBase
{
    /// <summary>
    /// Gets or sets the discriminator for TPH inheritance.
    /// </summary>
    public string SourceType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the data source.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of what this data source provides.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets whether this data source is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache duration in seconds (0 = no cache).
    /// </summary>
    public int CacheDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the ID of the Flow Task template that owns this data source.
    /// </summary>
    public Guid? FlowTaskTemplateId { get; set; }

    // Navigation properties
    /// <summary>
    /// Gets or sets the Flow Task template that owns this data source.
    /// </summary>
    public virtual FlowTaskTemplate? FlowTaskTemplate { get; set; }
}