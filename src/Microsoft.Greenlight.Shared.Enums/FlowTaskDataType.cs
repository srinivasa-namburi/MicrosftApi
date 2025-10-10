// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Defines the data types for Flow Task requirement fields.
/// </summary>
public enum FlowTaskDataType
{
    /// <summary>
    /// Unknown or unspecified data type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Plain text or string value.
    /// </summary>
    Text = 1,

    /// <summary>
    /// Multi-line text area.
    /// </summary>
    TextArea = 2,

    /// <summary>
    /// Numeric value (integer or decimal).
    /// </summary>
    Number = 3,

    /// <summary>
    /// Date value (without time).
    /// </summary>
    Date = 4,

    /// <summary>
    /// Date and time value.
    /// </summary>
    DateTime = 5,

    /// <summary>
    /// Boolean (true/false, yes/no).
    /// </summary>
    Boolean = 6,

    /// <summary>
    /// Single choice from a predefined list.
    /// </summary>
    Choice = 7,

    /// <summary>
    /// Multiple choices from a predefined list.
    /// </summary>
    MultiChoice = 8,

    /// <summary>
    /// Email address.
    /// </summary>
    Email = 9,

    /// <summary>
    /// Phone number.
    /// </summary>
    Phone = 10,

    /// <summary>
    /// URL/web address.
    /// </summary>
    Url = 11,

    /// <summary>
    /// File upload reference.
    /// </summary>
    File = 12,

    /// <summary>
    /// JSON object or structured data.
    /// </summary>
    Json = 13
}
