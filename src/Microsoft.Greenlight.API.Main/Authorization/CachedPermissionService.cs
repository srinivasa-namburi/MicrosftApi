// Copyright (c) Microsoft Corporation. All rights reserved.

using LazyCache;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Cached permission service implementation using LazyCache for performance optimization.
/// Caches user permissions and role mappings to avoid database hits on every authorization check.
/// </summary>
public sealed class CachedPermissionService : ICachedPermissionService
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly IAppCache _cache;
    private readonly ILogger<CachedPermissionService> _logger;
    private readonly TimeSpan _userPermissionsCacheExpiry = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _rolePermissionsCacheExpiry = TimeSpan.FromMinutes(30);
    
    // Cache key patterns
    private const string UserPermissionsCacheKeyPattern = "user_permissions:{0}";
    private const string RolePermissionsCacheKey = "role_permissions_mapping";
    private const string AllPermissionsCacheKey = "all_permissions";

    public CachedPermissionService(
        IDbContextFactory<DocGenerationDbContext> dbContextFactory,
        IAppCache cache,
        ILogger<CachedPermissionService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Checks if a user has a specific permission using cached data.
    /// </summary>
    public async Task<bool> UserHasPermissionAsync(string providerSubjectId, string permissionKey, CancellationToken cancellationToken = default)
    {
        var userPermissions = await GetUserPermissionsAsync(providerSubjectId, cancellationToken);
        return userPermissions.Contains(permissionKey);
    }

    /// <summary>
    /// Gets all permissions for a user, using cache when possible.
    /// </summary>
    public async Task<HashSet<string>> GetUserPermissionsAsync(string providerSubjectId, CancellationToken cancellationToken = default)
    {
        var cacheKey = string.Format(UserPermissionsCacheKeyPattern, providerSubjectId);
        
        return await _cache.GetOrAddAsync(cacheKey, async () =>
        {
            _logger.LogDebug("Loading user permissions from database for user {ProviderSubjectId}", providerSubjectId);
            
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            // Get user's roles and their associated permissions
            var permissions = await db.UserRoles
                .Where(ur => ur.ProviderSubjectId == providerSubjectId)
                .Join(db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp)
                .Join(db.Permissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p)
                .Where(p => p.IsActive)
                .Select(p => p.Key)
                .ToListAsync(cancellationToken);

            return new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }, _userPermissionsCacheExpiry);
    }

    /// <summary>
    /// Synchronizes a user's Entra role assignments and returns their current permissions.
    /// This method handles the Entra token synchronization logic and updates the cache.
    /// Includes fallback mechanism for DocumentGeneration role.
    /// </summary>
    public async Task<HashSet<string>> SyncUserRolesAndGetPermissionsAsync(
        string providerSubjectId, 
        HashSet<string> tokenRoleNames, 
        HashSet<Guid> tokenRoleIds, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Syncing Entra roles and getting permissions for user {ProviderSubjectId}", providerSubjectId);
        
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        
        // Load all roles that have Entra mapping
        var mappedRoles = await GetRolePermissionsMappingAsync(cancellationToken);
        
        var hasChanges = false;
        var userAssignedFromEntra = false;

        if (mappedRoles.Count > 0)
        {
            // Primary mapping: by EntraAppRoleValue (role names from 'roles' claim)
            var mappedByName = mappedRoles
                .Where(r => !string.IsNullOrEmpty(r.Value.EntraAppRoleValue))
                .ToDictionary(r => r.Value.EntraAppRoleValue!, r => r.Value, StringComparer.OrdinalIgnoreCase);
            
            // Secondary mapping: by EntraAppRoleId (role IDs from 'xms_roles' claim) - if available
            var mappedById = mappedRoles
                .Where(r => r.Value.EntraAppRoleId.HasValue)
                .ToDictionary(r => r.Value.EntraAppRoleId!.Value, r => r.Value);

            // Current assignments
            var existingAssignments = await db.UserRoles
                .Where(ur => ur.ProviderSubjectId == providerSubjectId)
                .ToListAsync(cancellationToken);

            // Primary: Add/update from token by role name mapping (most common)
            foreach (var roleName in tokenRoleNames)
            {
                if (mappedByName.TryGetValue(roleName, out var roleInfo))
                {
                    if (!existingAssignments.Any(a => a.RoleId == roleInfo.Id && a.IsFromEntra))
                    {
                        db.UserRoles.Add(new GreenlightUserRole
                        {
                            Id = Guid.NewGuid(),
                            ProviderSubjectId = providerSubjectId,
                            RoleId = roleInfo.Id,
                            IsFromEntra = true
                        });
                        hasChanges = true;
                        userAssignedFromEntra = true;
                        _logger.LogDebug("Added Entra role {RoleName} to user {ProviderSubjectId} via role name mapping", roleInfo.Name, providerSubjectId);
                    }
                    else
                    {
                        userAssignedFromEntra = true;
                    }
                }
            }

            // Secondary: Add/update from token by GUID mapping (if available and not already matched)
            foreach (var roleId in tokenRoleIds)
            {
                if (mappedById.TryGetValue(roleId, out var roleInfo))
                {
                    // Only add if not already added by name mapping
                    if (!existingAssignments.Any(a => a.RoleId == roleInfo.Id && a.IsFromEntra))
                    {
                        db.UserRoles.Add(new GreenlightUserRole
                        {
                            Id = Guid.NewGuid(),
                            ProviderSubjectId = providerSubjectId,
                            RoleId = roleInfo.Id,
                            IsFromEntra = true
                        });
                        hasChanges = true;
                        userAssignedFromEntra = true;
                        _logger.LogDebug("Added Entra role {RoleName} to user {ProviderSubjectId} via role ID mapping", roleInfo.Name, providerSubjectId);
                    }
                    else
                    {
                        userAssignedFromEntra = true;
                    }
                }
            }

            // Remove Entra-sourced assignments that aren't in token anymore
            foreach (var existing in existingAssignments.Where(a => a.IsFromEntra).ToList())
            {
                if (mappedRoles.TryGetValue(existing.RoleId, out var roleInfo))
                {
                    var shouldRemove = true;
                    
                    // Check if role is still present in token by name
                    if (!string.IsNullOrEmpty(roleInfo.EntraAppRoleValue) && 
                        tokenRoleNames.Contains(roleInfo.EntraAppRoleValue, StringComparer.OrdinalIgnoreCase))
                    {
                        shouldRemove = false;
                    }
                    
                    // Check if role is still present in token by ID (fallback)
                    if (shouldRemove && roleInfo.EntraAppRoleId.HasValue && 
                        tokenRoleIds.Contains(roleInfo.EntraAppRoleId.Value))
                    {
                        shouldRemove = false;
                    }
                    
                    if (shouldRemove)
                    {
                        db.UserRoles.Remove(existing);
                        hasChanges = true;
                        _logger.LogDebug("Removed Entra role {RoleName} from user {ProviderSubjectId}", roleInfo.Name, providerSubjectId);
                    }
                }
            }
        }

        // Fallback mechanism: If user has DocumentGeneration role but no mapped assignments
        // This handles cases where DocumentGeneration role exists but isn't mapped properly
        if (!userAssignedFromEntra && tokenRoleNames.Contains("DocumentGeneration", StringComparer.OrdinalIgnoreCase))
        {
            var existingAssignments = await db.UserRoles
                .Where(ur => ur.ProviderSubjectId == providerSubjectId)
                .ToListAsync(cancellationToken);

            // Check if user has any role at all
            if (!existingAssignments.Any())
            {
                // Assign FullAccess role as fallback for DocumentGeneration users
                db.UserRoles.Add(new GreenlightUserRole
                {
                    Id = Guid.NewGuid(),
                    ProviderSubjectId = providerSubjectId,
                    RoleId = AuthorizationIds.Roles.FullAccess,
                    IsFromEntra = false // Mark as local assignment since it's a fallback
                });
                hasChanges = true;
                _logger.LogInformation("Applied fallback FullAccess role for user {ProviderSubjectId} with DocumentGeneration Entra role but no explicit mapping", providerSubjectId);
            }
        }

        if (hasChanges)
        {
            await db.SaveChangesAsync(cancellationToken);
            // Invalidate the user's cached permissions since roles changed
            await InvalidateUserPermissionsAsync(providerSubjectId);
        }

        // Return the user's current permissions (this will reload from cache or database)
        return await GetUserPermissionsAsync(providerSubjectId, cancellationToken);
    }

    /// <summary>
    /// Gets a cached mapping of role ID to role information with permissions.
    /// </summary>
    private async Task<Dictionary<Guid, RoleInfo>> GetRolePermissionsMappingAsync(CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrAddAsync(RolePermissionsCacheKey, async () =>
        {
            _logger.LogDebug("Loading role permissions mapping from database");
            
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            
            var roles = await db.Roles
                .Where(r => r.IsActive)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync(cancellationToken);

            return roles.ToDictionary(r => r.Id, r => new RoleInfo
            {
                Id = r.Id,
                Name = r.Name,
                EntraAppRoleId = r.EntraAppRoleId,
                EntraAppRoleValue = r.EntraAppRoleValue,
                Permissions = r.RolePermissions
                    .Where(rp => rp.Permission.IsActive)
                    .Select(rp => rp.Permission.Key)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
            });
        }, _rolePermissionsCacheExpiry);
    }

    /// <summary>
    /// Invalidates the cached permissions for a specific user.
    /// </summary>
    public async Task InvalidateUserPermissionsAsync(string providerSubjectId)
    {
        var cacheKey = string.Format(UserPermissionsCacheKeyPattern, providerSubjectId);
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cached permissions for user {ProviderSubjectId}", providerSubjectId);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Invalidates all cached permission data (when roles or permissions change globally).
    /// </summary>
    public async Task InvalidateAllPermissionsAsync()
    {
        // Remove role mappings cache
        _cache.Remove(RolePermissionsCacheKey);
        _cache.Remove(AllPermissionsCacheKey);
        
        // Note: We can't easily remove all user permission caches without knowing all user IDs
        // LazyCache doesn't have a pattern-based removal, so we rely on expiration for user caches
        // In a production system, you might want to use a more sophisticated cache like Redis
        // that supports pattern-based key deletion
        
        _logger.LogInformation("Invalidated all cached permission data");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Simple role info for caching purposes.
    /// </summary>
    private sealed class RoleInfo
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? EntraAppRoleId { get; set; }
        public string? EntraAppRoleValue { get; set; }
        public HashSet<string> Permissions { get; set; } = new();
    }
}