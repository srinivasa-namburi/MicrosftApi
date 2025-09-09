// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

/// <summary>
/// Request sent during first login to upsert the user and synchronize Entra roles
/// into Greenlight role assignments, applying fallback rules for DocumentGeneration.
/// </summary>
public sealed class FirstLoginSyncRequest
{
    /// <summary>
    /// The user's provider subject identifier (e.g., AAD oid/sub).
    /// </summary>
    public required string ProviderSubjectId { get; set; }

    /// <summary>
    /// The user's full display name.
    /// </summary>
    public required string FullName { get; set; }

    /// <summary>
    /// The user's email or preferred username.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Role names from the Entra token ("roles" claim).
    /// </summary>
    public List<string> TokenRoleNames { get; set; } = new();

    /// <summary>
    /// Role IDs from the Entra token ("xms_roles" claim).
    /// </summary>
    public List<Guid> TokenRoleIds { get; set; } = new();

    /// <summary>
    /// Optional: caller's trusted client IDs (for diagnostics only). The API enforces
    /// trusted callers using the authenticated token's client/app ID and server configuration.
    /// </summary>
    public List<string>? TrustedCallerClientIds { get; set; }
}
