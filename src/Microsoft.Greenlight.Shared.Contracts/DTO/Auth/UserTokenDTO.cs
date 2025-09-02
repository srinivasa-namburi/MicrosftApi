// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Auth;

/// <summary>
/// DTO used to upsert or retrieve a user's access token used for downstream services (e.g., MCP servers).
/// </summary>
public sealed class UserTokenDTO
{
    /// <summary>
    /// Provider subject identifier for the user (stable unique id from IdP).
    /// </summary>
    [Required]
    public string ProviderSubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Opaque bearer token for the user. This is stored encrypted at rest by the grain state provider if configured; otherwise persisted as-is.
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional expiration time (UTC) of the token. If null, the grain will not attempt proactive refresh.
    /// </summary>
    public DateTimeOffset? ExpiresOnUtc { get; set; }
}
