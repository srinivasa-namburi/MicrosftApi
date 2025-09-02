// Copyright (c) Microsoft Corporation. All rights reserved.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Evaluates whether the current user has ANY of the requested permissions via Greenlight roles.
/// Uses cached permission service for improved performance and handles Entra App Role synchronization.
/// </summary>
public sealed class RequiresAnyPermissionHandler : AuthorizationHandler<RequiresAnyPermissionRequirement>
{
    private readonly ICachedPermissionService _cachedPermissionService;
    private readonly ILogger<RequiresAnyPermissionHandler> _logger;

    public RequiresAnyPermissionHandler(
        ICachedPermissionService cachedPermissionService,
        ILogger<RequiresAnyPermissionHandler> logger)
    {
        _cachedPermissionService = cachedPermissionService;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RequiresAnyPermissionRequirement requirement)
    {
        var user = context.User;
        _logger.LogDebug("Authorization check starting for permissions: {PermissionKeys}, User authenticated: {IsAuthenticated}",
            string.Join(", ", requirement.PermissionKeys), user?.Identity?.IsAuthenticated);

        if (user?.Identity?.IsAuthenticated != true)
        {
            _logger.LogDebug("User is not authenticated, denying access");
            return; // not satisfied
        }

        // Determine ProviderSubjectId (we use oid if present, fallback to sub)
        var providerSubjectId = user.FindFirstValue("oid") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");

        if (string.IsNullOrEmpty(providerSubjectId))
        {
            _logger.LogWarning("Could not determine ProviderSubjectId from user claims");
            return;
        }

        _logger.LogDebug("ProviderSubjectId resolved to: {ProviderSubjectId}", providerSubjectId);

        try
        {
            // Extract Entra app role assignments from token
            // Primary: Role names from 'roles' claim (most commonly available)
            var tokenRoleNames = user.FindAll("roles").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            
            // Secondary: Role IDs from 'xms_roles' claim (less commonly available)
            var tokenRoleIds = user
                .FindAll("xms_roles")
                .Select(c => c.Value)
                .Select(v => Guid.TryParse(v, out var gid) ? (Guid?)gid : null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            _logger.LogDebug("User {ProviderSubjectId} has {RoleNameCount} role names and {RoleIdCount} role IDs from token", 
                providerSubjectId, tokenRoleNames.Count, tokenRoleIds.Count);

            // Sync user roles with Entra and get current permissions (uses caching)
            var userPermissions = await _cachedPermissionService.SyncUserRolesAndGetPermissionsAsync(
                providerSubjectId, tokenRoleNames, tokenRoleIds, CancellationToken.None);

            // Check if user has ANY of the required permissions (OR logic)
            var matchingPermissions = requirement.PermissionKeys.Where(userPermissions.Contains).ToList();
            var hasAnyPermission = matchingPermissions.Count > 0;

            _logger.LogDebug("User {ProviderSubjectId} has any of permissions {PermissionKeys}: {HasPermission} (matching: {MatchingPermissions}, from {PermissionCount} cached permissions)",
                providerSubjectId, string.Join(", ", requirement.PermissionKeys), hasAnyPermission, 
                string.Join(", ", matchingPermissions), userPermissions.Count);

            if (hasAnyPermission)
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during permission check for user {ProviderSubjectId} and permissions {PermissionKeys}",
                providerSubjectId, string.Join(", ", requirement.PermissionKeys));
            // Fail closed on errors
        }
    }
}