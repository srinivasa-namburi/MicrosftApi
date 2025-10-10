// Copyright (c) Microsoft Corporation. All rights reserved.
using System.Diagnostics.Metrics;

namespace Microsoft.Greenlight.McpServer.Flow.Monitoring;

/// <summary>
/// Metrics for MCP server components.
/// </summary>
internal static class McpMetrics
{
    private static readonly Meter Meter = new Meter("Microsoft.Greenlight.McpServer", "1.0.0");

    /// <summary>
    /// Counter incremented when secret-based authentication succeeds.
    /// </summary>
    public static readonly Counter<long> SecretAuthSuccess = Meter.CreateCounter<long>("mcp.secret_auth.success");

    /// <summary>
    /// Counter incremented when secret-based authentication fails.
    /// </summary>
    public static readonly Counter<long> SecretAuthFailure = Meter.CreateCounter<long>("mcp.secret_auth.failure");
}

