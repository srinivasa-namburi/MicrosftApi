// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Client-side service for checking user permissions.
/// Provides cached permission lookups and permission-based UI control.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if the current user has a specific permission.
    /// </summary>
    /// <param name="permissionKey">The permission key to check.</param>
    /// <returns>True if the user has the permission, false otherwise.</returns>
    Task<bool> HasPermissionAsync(string permissionKey);

    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    /// <param name="permissionKeys">The permission keys to check.</param>
    /// <returns>True if the user has any of the permissions, false otherwise.</returns>
    Task<bool> HasAnyPermissionAsync(params string[] permissionKeys);

    /// <summary>
    /// Gets all permissions for the current user.
    /// </summary>
    /// <returns>A set of permission keys the user has.</returns>
    Task<HashSet<string>> GetUserPermissionsAsync();

    /// <summary>
    /// Invalidates the permission cache for the current user.
    /// Should be called when permissions might have changed.
    /// </summary>
    Task InvalidatePermissionCacheAsync();

    /// <summary>
    /// Gets the current user's provider subject ID.
    /// </summary>
    /// <returns>The provider subject ID or null if not authenticated.</returns>
    Task<string?> GetCurrentUserSubjectIdAsync();
}