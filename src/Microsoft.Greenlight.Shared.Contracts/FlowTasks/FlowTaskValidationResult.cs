// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Collections.Generic;

namespace Microsoft.Greenlight.Shared.Contracts.FlowTasks;

/// <summary>
/// Represents the result of validating Flow Task requirements.
/// </summary>
public class FlowTaskValidationResult
{
    /// <summary>
    /// Gets or sets whether all requirements are valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation errors.
    /// </summary>
    public List<FlowTaskValidationError> Errors { get; set; } = new List<FlowTaskValidationError>();

    /// <summary>
    /// Gets or sets the missing required fields.
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets any validation warnings.
    /// </summary>
    public List<string> Warnings { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether the Flow Task can proceed despite validation issues.
    /// </summary>
    public bool CanProceed { get; set; }
}

/// <summary>
/// Represents a validation error for a Flow Task requirement.
/// </summary>
public class FlowTaskValidationError
{
    /// <summary>
    /// Gets or sets the field name that has the error.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the severity of the error.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;
}

/// <summary>
/// Represents the severity of a validation issue.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent proceeding.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that must be resolved.
    /// </summary>
    Error,

    /// <summary>
    /// Critical error that stops execution.
    /// </summary>
    Critical
}