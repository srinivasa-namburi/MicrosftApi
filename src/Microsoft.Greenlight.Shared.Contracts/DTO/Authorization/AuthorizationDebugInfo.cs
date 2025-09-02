// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;

/// <summary>
/// Authorization debug information for a user.
/// </summary>
public sealed class AuthorizationDebugInfo
{
    /// <summary>
    /// Whether the user is authenticated.
    /// </summary>
    public bool? IsAuthenticated { get; set; }
    
    /// <summary>
    /// The user's provider subject ID.
    /// </summary>
    public string? ProviderSubjectId { get; set; }
    
    /// <summary>
    /// The user's claims from the token.
    /// </summary>
    public List<ClaimInfo> Claims { get; set; } = new();
    
    /// <summary>
    /// The number of roles assigned to the user.
    /// </summary>
    public int UserRoleCount { get; set; }
    
    /// <summary>
    /// The user's role assignments and details.
    /// </summary>
    public List<UserRoleDebugInfo> UserRoles { get; set; } = new();
}

/// <summary>
/// Information about a user's claim.
/// </summary>
public sealed class ClaimInfo
{
    /// <summary>
    /// The claim type.
    /// </summary>
    public required string Type { get; set; }
    
    /// <summary>
    /// The claim value.
    /// </summary>
    public required string Value { get; set; }
}

/// <summary>
/// Debug information about a user's role assignment.
/// </summary>
public sealed class UserRoleDebugInfo
{
    /// <summary>
    /// The role assignment details.
    /// </summary>
    public UserRoleAssignmentDebugInfo? RoleAssignment { get; set; }
    
    /// <summary>
    /// The role details.
    /// </summary>
    public RoleDebugInfo? Role { get; set; }
    
    /// <summary>
    /// The number of permissions granted by this role.
    /// </summary>
    public int PermissionCount { get; set; }
    
    /// <summary>
    /// The permissions granted by this role.
    /// </summary>
    public List<PermissionDebugInfo> Permissions { get; set; } = new();
}

/// <summary>
/// Debug information about a role assignment.
/// </summary>
public sealed class UserRoleAssignmentDebugInfo
{
    /// <summary>
    /// The assignment ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The role ID.
    /// </summary>
    public Guid RoleId { get; set; }
    
    /// <summary>
    /// Whether the assignment is from Entra.
    /// </summary>
    public bool IsFromEntra { get; set; }
    
    /// <summary>
    /// When the role was assigned.
    /// </summary>
    public DateTime AssignedUtc { get; set; }
}

/// <summary>
/// Debug information about a role.
/// </summary>
public sealed class RoleDebugInfo
{
    /// <summary>
    /// The role ID.
    /// </summary>
    public Guid Id { get; set; }
    
    /// <summary>
    /// The role name.
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// The role description.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Whether the role is active.
    /// </summary>
    public bool IsActive { get; set; }
}

/// <summary>
/// Debug information about a permission.
/// </summary>
public sealed class PermissionDebugInfo
{
    /// <summary>
    /// The permission key.
    /// </summary>
    public required string Key { get; set; }
    
    /// <summary>
    /// The permission display name.
    /// </summary>
    public required string DisplayName { get; set; }
    
    /// <summary>
    /// Whether the permission is active.
    /// </summary>
    public bool IsActive { get; set; }
}