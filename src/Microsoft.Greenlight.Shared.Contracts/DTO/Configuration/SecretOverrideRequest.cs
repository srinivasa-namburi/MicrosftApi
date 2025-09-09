// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Configuration;

/// <summary>
/// Represents a request to override a secret configuration value.
/// </summary>
public class SecretOverrideRequest
{
    /// <summary>
    /// The configuration key to override (e.g., "ServiceConfiguration:AzureMaps:Key").
    /// </summary>
    public string ConfigurationKey { get; set; } = string.Empty;

    /// <summary>
    /// The new secret value to set. The current value is never returned for security.
    /// </summary>
    public string SecretValue { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the override for audit purposes.
    /// </summary>
    public string? Description { get; set; }
}