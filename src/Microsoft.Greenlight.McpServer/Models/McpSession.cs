// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.McpServer.Models;

/// <summary>
/// Represents a persisted MCP session stored in cache (Redis-backed).
/// </summary>
public class McpSession
{
    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    [Required]
    public Guid SessionId { get; set; }

    /// <summary>
    /// Gets or sets the effective user object ID associated with the session.
    /// </summary>
    [Required]
    public string UserObjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the session will expire (based on last refresh).
    /// </summary>
    public DateTime ExpiresUtc { get; set; }
}

