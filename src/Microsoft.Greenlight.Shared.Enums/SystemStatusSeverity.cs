// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Severity levels for system status updates.
/// </summary>
public enum SystemStatusSeverity
{
    /// <summary>Informational status update</summary>
    Info,
    /// <summary>Warning that should be noticed</summary>
    Warning,
    /// <summary>Critical issue requiring attention</summary>
    Critical
}