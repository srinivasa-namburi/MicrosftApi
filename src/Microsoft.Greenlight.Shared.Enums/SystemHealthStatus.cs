// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Overall system health status.
/// </summary>
public enum SystemHealthStatus
{
    /// <summary>All systems operating normally</summary>
    Healthy,
    /// <summary>Some warnings but system is functional</summary>
    Warning,
    /// <summary>Critical issues affecting system functionality</summary>
    Critical,
    /// <summary>System status cannot be determined</summary>
    Unknown
}