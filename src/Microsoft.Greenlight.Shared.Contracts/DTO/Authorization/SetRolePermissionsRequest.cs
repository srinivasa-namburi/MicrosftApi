// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Request to set permissions assigned to a role.
/// </summary>
public sealed class SetRolePermissionsRequest
{
    /// <summary>
    /// The role identifier.
    /// </summary>
    public Guid RoleId { get; set; }
    
    /// <summary>
    /// The collection of permission keys assigned to the role.
    /// </summary>
    public List<string> PermissionKeys { get; set; } = new();
}