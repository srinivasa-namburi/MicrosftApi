// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Greenlight.Web.DocGen.Client.ServiceClients;
using Microsoft.Greenlight.Web.Shared.Auth;
using Microsoft.Greenlight.Web.Shared.ServiceClients;
using System.Collections.Concurrent;

namespace Microsoft.Greenlight.Web.DocGen.Client.Services;

/// <summary>
/// Client-side service for checking user permissions with in-memory caching.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private readonly IAdminAuthorizationApiClient _adminAuthorizationApiClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<PermissionService> _logger;
    
    // Cache permissions for the current user session
    private readonly ConcurrentDictionary<string, CachedPermissions> _permissionCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromMinutes(10); // Cache for 10 minutes

    public PermissionService(
        IAdminAuthorizationApiClient adminAuthorizationApiClient,
        AuthenticationStateProvider authStateProvider,
        ILogger<PermissionService> logger)
    {
        _adminAuthorizationApiClient = adminAuthorizationApiClient;
        _authStateProvider = authStateProvider;
        _logger = logger;
    }

    public async Task<bool> HasPermissionAsync(string permissionKey)
    {
        try
        {
            var permissions = await GetUserPermissionsAsync();
            return permissions.Contains(permissionKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking permission {PermissionKey}, denying access", permissionKey);
            return false;
        }
    }

    public async Task<bool> HasAnyPermissionAsync(params string[] permissionKeys)
    {
        try
        {
            var permissions = await GetUserPermissionsAsync();
            return permissionKeys.Any(pk => permissions.Contains(pk));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking permissions {PermissionKeys}, denying access", string.Join(", ", permissionKeys));
            return false;
        }
    }

    public async Task<HashSet<string>> GetUserPermissionsAsync()
    {
        var subjectId = await GetCurrentUserSubjectIdAsync();
        if (string.IsNullOrEmpty(subjectId))
        {
            return new HashSet<string>();
        }

        // Check cache first
        if (_permissionCache.TryGetValue(subjectId, out var cached) && 
            DateTime.UtcNow < cached.ExpiresAt)
        {
            return cached.Permissions;
        }

        try
        {
            // Try to get permissions through the debug endpoint (most reliable)
            var debugInfo = await _adminAuthorizationApiClient.GetAuthorizationDebugInfoAsync();
            var permissions = new HashSet<string>();

            if (debugInfo?.UserRoles != null)
            {
                foreach (var userRole in debugInfo.UserRoles)
                {
                    if (userRole.Permissions != null)
                    {
                        foreach (var permission in userRole.Permissions)
                        {
                            if (permission.IsActive)
                            {
                                permissions.Add(permission.Key);
                            }
                        }
                    }
                }
            }

            // Cache the result
            _permissionCache.AddOrUpdate(subjectId, 
                new CachedPermissions(permissions, DateTime.UtcNow.Add(_cacheExpiry)),
                (key, oldValue) => new CachedPermissions(permissions, DateTime.UtcNow.Add(_cacheExpiry)));

            _logger.LogDebug("Cached {PermissionCount} permissions for user {SubjectId}", permissions.Count, subjectId);
            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch permissions for user {SubjectId}, returning empty set", subjectId);
            
            // Cache empty result with shorter expiry to avoid hammering the API
            _permissionCache.AddOrUpdate(subjectId, 
                new CachedPermissions(new HashSet<string>(), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1))),
                (key, oldValue) => new CachedPermissions(new HashSet<string>(), DateTime.UtcNow.Add(TimeSpan.FromMinutes(1))));
            
            return new HashSet<string>();
        }
    }

    public async Task InvalidatePermissionCacheAsync()
    {
        var subjectId = await GetCurrentUserSubjectIdAsync();
        if (!string.IsNullOrEmpty(subjectId))
        {
            _permissionCache.TryRemove(subjectId, out _);
            _logger.LogDebug("Invalidated permission cache for user {SubjectId}", subjectId);
        }
    }

    public async Task<string?> GetCurrentUserSubjectIdAsync()
    {
        try
        {
            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (authState?.User?.Identity?.IsAuthenticated == true)
            {
                var userInfo = UserInfo.FromClaimsPrincipal(authState.User);
                return userInfo.UserId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error getting current user subject ID");
        }
        
        return null;
    }

    private sealed record CachedPermissions(HashSet<string> Permissions, DateTime ExpiresAt);
}