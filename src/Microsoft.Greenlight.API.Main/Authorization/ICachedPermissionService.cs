// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Cached permission service that provides fast lookups for user permissions and role mappings.
/// </summary>
public interface ICachedPermissionService
{
    /// <summary>
    /// Checks if a user has a specific permission.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject ID.</param>
    /// <param name="permissionKey">The permission key to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has the permission, false otherwise.</returns>
    Task<bool> UserHasPermissionAsync(string providerSubjectId, string permissionKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all permissions for a user.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A set of permission keys the user has.</returns>
    Task<HashSet<string>> GetUserPermissionsAsync(string providerSubjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached permissions for a specific user.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject ID.</param>
    Task InvalidateUserPermissionsAsync(string providerSubjectId);

    /// <summary>
    /// Invalidates the cached permissions for all users (e.g., when roles or permissions change).
    /// </summary>
    Task InvalidateAllPermissionsAsync();

    /// <summary>
    /// Synchronizes a user's Entra role assignments and returns their current permissions.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject ID.</param>
    /// <param name="tokenRoleNames">Role names from the user's token.</param>
    /// <param name="tokenRoleIds">Role IDs from the user's token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A set of permission keys the user has after synchronization.</returns>
    Task<HashSet<string>> SyncUserRolesAndGetPermissionsAsync(string providerSubjectId, HashSet<string> tokenRoleNames, HashSet<Guid> tokenRoleIds, CancellationToken cancellationToken = default);
}