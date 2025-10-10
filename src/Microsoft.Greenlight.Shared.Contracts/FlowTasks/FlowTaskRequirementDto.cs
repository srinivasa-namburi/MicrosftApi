// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// DTO for Flow Task requirement information.
/// </summary>
public class FlowTaskRequirementDto
{
    /// <summary>
    /// Gets or sets the unique identifier of the requirement.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the ID of the parent section.
    /// </summary>
    public Guid FlowTaskSectionId { get; set; }

    /// <summary>
    /// Gets or sets the field name (used as identifier in processing).
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name shown to users.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description or help text for this requirement.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the data type of this requirement (e.g., text, number, date, choice).
    /// </summary>
    public string DataType { get; set; } = "text";

    /// <summary>
    /// Gets or sets whether this requirement is required.
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this requirement should be collected from a data source.
    /// </summary>
    public bool IsDataSourced { get; set; }

    /// <summary>
    /// Gets or sets the ID of the data source to use for this requirement.
    /// </summary>
    public Guid? DataSourceId { get; set; }

    /// <summary>
    /// Gets or sets the validation rules as JSON.
    /// </summary>
    public string? ValidationRulesJson { get; set; }

    /// <summary>
    /// Gets or sets valid options for choice fields as JSON array.
    /// </summary>
    public string? ValidOptionsJson { get; set; }

    /// <summary>
    /// Gets or sets the default value for this requirement.
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the prompt template for collecting this requirement.
    /// </summary>
    public string? PromptTemplate { get; set; }

    /// <summary>
    /// Gets or sets the order in which this requirement appears within its section.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Gets or sets conditional logic for when this requirement should be shown (JSON).
    /// </summary>
    public string? ConditionalLogicJson { get; set; }
}
