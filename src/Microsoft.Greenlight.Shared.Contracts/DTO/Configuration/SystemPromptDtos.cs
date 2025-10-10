// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

/// <summary>
/// Response model for system prompt endpoints.
/// </summary>
public class SystemPromptResponse
{
    /// <summary>
    /// Gets or sets the prompt name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prompt text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this prompt has been customized (database override exists).
    /// </summary>
    public bool IsCustomized { get; set; }
}

/// <summary>
/// Request model for updating a system prompt.
/// </summary>
public class UpdateSystemPromptRequest
{
    /// <summary>
    /// Gets or sets the prompt text.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this prompt is active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Request model for validating a prompt.
/// </summary>
public class ValidatePromptRequest
{
    /// <summary>
    /// Gets or sets the prompt text to validate.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// Result of prompt validation.
/// </summary>
public class PromptValidationResult
{
    /// <summary>
    /// Gets or sets whether the prompt is valid.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the list of missing required variables.
    /// </summary>
    public List<string> MissingVariables { get; set; } = new List<string>();
}
