// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

/// <summary>
/// Represents information about a secret configuration override without exposing the actual secret value.
/// </summary>
public class SecretOverrideInfo
{
    /// <summary>
    /// The configuration key being overridden.
    /// </summary>
    public string ConfigurationKey { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this key has been overridden (true) or uses the default value (false).
    /// </summary>
    public bool IsOverridden { get; set; }

    /// <summary>
    /// Indicates if the current effective value is non-empty.
    /// </summary>
    public bool HasValue { get; set; }

    /// <summary>
    /// Optional description of the override for audit purposes.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// When the override was last updated (if overridden).
    /// </summary>
    public DateTime? LastUpdated { get; set; }
}