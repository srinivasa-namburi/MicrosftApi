// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// Base DTO for Flow Task data sources.
/// </summary>
public class FlowTaskDataSourceDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the data source.
    /// </summary>
    public Guid Id { get; set; }

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
}
