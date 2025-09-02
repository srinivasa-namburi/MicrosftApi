// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Permission modes for controlling how unauthorized content is handled.
/// </summary>
public enum PermissionMode
{
    /// <summary>
    /// Show content when authorized, hide when not authorized (default).
    /// </summary>
    ShowHide,

    /// <summary>
    /// Show content when authorized, disable controls when not authorized.
    /// This preserves layout while making controls non-interactive.
    /// </summary>
    DisableControls,

    /// <summary>
    /// Show content when authorized, preserve layout space when not authorized.
    /// Renders an invisible placeholder to maintain layout consistency.
    /// </summary>
    PreserveLayout
}
