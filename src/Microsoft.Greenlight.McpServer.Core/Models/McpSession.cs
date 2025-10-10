// Copyright (c) Microsoft Corporation. All rights reserved.
using System.ComponentModel.DataAnnotations;

namespace Microsoft.Greenlight.McpServer.Core.Models;

/// <summary>
/// Represents a persisted MCP session stored in cache (Redis-backed).
/// </summary>
public class McpSession
{
    /// <summary>
    /// Gets or sets the unique MCP session identifier (from Mcp-Session-Id header).
    /// This is a string containing visible ASCII characters, not a GUID.
    /// </summary>
    [Required]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ProviderSubjectId (from "sub" claim) associated with the session.
    /// This is the stable user identifier used throughout the system.
    /// </summary>
    [Required]
    public string ProviderSubjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the session will expire (based on last refresh).
    /// </summary>
    public DateTime ExpiresUtc { get; set; }

    private Dictionary<string, string>? _sessionData;

    /// <summary>
    /// Gets or sets the session data dictionary for storing arbitrary key/value pairs.
    /// Values are stored as strings - callers should serialize complex objects (e.g., to JSON) as needed.
    /// </summary>
    public Dictionary<string, string> SessionData
    {
        get => _sessionData ??= new Dictionary<string, string>();
        set => _sessionData = value;
    }
}

