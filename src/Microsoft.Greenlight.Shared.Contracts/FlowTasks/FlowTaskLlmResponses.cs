// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// Represents a single extracted field with name and value.
/// </summary>
public class ExtractedField
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the extracted value as a string.
    /// </summary>
    public required string Value { get; set; }
}

/// <summary>
/// Response contract for field value extraction from user messages.
/// Used by ExtractFieldValuesAsync for structured LLM output.
/// </summary>
public class ExtractedFieldsResponse
{
    /// <summary>
    /// Gets or sets the list of extracted field values.
    /// </summary>
    public required List<ExtractedField> Fields { get; set; }
}

/// <summary>
/// Response contract for generating requirement prompts.
/// Used by GenerateRequirementPromptAsync for structured LLM output.
/// </summary>
public class RequirementPromptResponse
{
    /// <summary>
    /// Gets or sets the prompt text to show to the user for collecting a requirement value.
    /// </summary>
    public required string PromptText { get; set; }

    /// <summary>
    /// Gets or sets suggested example values or formats to help the user.
    /// </summary>
    public List<string>? Examples { get; set; }
}

/// <summary>
/// Response contract for offering optional fields to the user.
/// Used by GenerateOptionalFieldsOfferAsync for structured LLM output.
/// </summary>
public class OptionalFieldsOfferResponse
{
    /// <summary>
    /// Gets or sets the message offering the optional fields to the user.
    /// </summary>
    public required string OfferMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of optional field names being offered.
    /// </summary>
    public List<string>? OptionalFieldNames { get; set; }
}

/// <summary>
/// Represents a single field value provided by the user.
/// </summary>
public class FieldValue
{
    /// <summary>
    /// Gets or sets the field name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the field value.
    /// </summary>
    public required string Value { get; set; }
}

/// <summary>
/// Response contract for parsing user selections of optional fields.
/// Used by ParseOptionalFieldSelectionAsync for structured LLM output.
/// </summary>
public class FieldSelectionResponse
{
    /// <summary>
    /// Gets or sets the action to take: "skip", "select", "provide", or "unclear".
    /// </summary>
    public required string Action { get; set; }

    /// <summary>
    /// Gets or sets the list of field names the user wants to fill (when Action = "select").
    /// </summary>
    public List<string>? SelectedFields { get; set; }

    /// <summary>
    /// Gets or sets the directly provided field values (when Action = "provide").
    /// </summary>
    public List<FieldValue>? ProvidedValues { get; set; }
}
