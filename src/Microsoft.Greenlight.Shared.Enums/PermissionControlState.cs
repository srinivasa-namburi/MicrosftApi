// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Enums;

/// <summary>
/// Optional control state exposed to children or for diagnostics when disabling controls.
/// </summary>
public class PermissionControlState
{
    public bool IsAuthorized { get; set; } = true;
    public string? UnauthorizedReason { get; set; }
}
