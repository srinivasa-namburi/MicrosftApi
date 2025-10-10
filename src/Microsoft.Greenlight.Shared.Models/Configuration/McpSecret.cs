// Copyright (c) Microsoft Corporation. All rights reserved.
using Microsoft.Greenlight.Shared.Models;

namespace Microsoft.Greenlight.Shared.Models.Configuration;

/// <summary>
/// Represents an API secret used to access the MCP server when no user bearer token is present.
/// Not stored in plaintext: only salted hash and salt are persisted.
/// </summary>
public sealed class McpSecret : EntityBase
{
    public McpSecret() : base() { }
    public McpSecret(Guid id) : base(id) { }

    /// <summary>
    /// Human-readable name for the secret (unique).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The salted hash of the secret value (Base64).
    /// </summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>
    /// The salt used for hashing (Base64).
    /// </summary>
    public string SecretSalt { get; set; } = string.Empty;

    /// <summary>
    /// The ProviderSubjectId (from "sub" claim) associated with this secret.
    /// This is the stable user identifier used throughout the system.
    /// </summary>
    public string ProviderSubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the secret is active and allowed for authentication.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Last time this secret was used for authentication.
    /// </summary>
    public DateTime? LastUsedUtc { get; set; }
}

