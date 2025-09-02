// Copyright (c) Microsoft Corporation. All rights reserved.

using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.API.Main.Authorization;
using Microsoft.Greenlight.Shared.Authorization;
using Microsoft.Greenlight.Shared.Contracts.DTO.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Authorization;
using System.Security.Claims;

namespace Microsoft.Greenlight.API.Main.Controllers;

/// <summary>
/// Admin endpoints to manage permissions, roles, and assignments.
/// Includes protection rules to ensure system integrity.
/// </summary>
public sealed class AdminAuthorizationController : BaseController
{
    private readonly DocGenerationDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILogger<AdminAuthorizationController> _logger;
    private readonly ICachedPermissionService _cachedPermissionService;
    private readonly AuthorizationProtectionService _protectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminAuthorizationController"/> class.
    /// </summary>
    /// <param name="db">The EF Core database context.</param>
    /// <param name="mapper">The AutoMapper instance.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="cachedPermissionService">The cached permission service.</param>
    /// <param name="protectionService">The authorization protection service.</param>
    public AdminAuthorizationController(
        DocGenerationDbContext db, 
        IMapper mapper, 
        ILogger<AdminAuthorizationController> logger,
        ICachedPermissionService cachedPermissionService,
        AuthorizationProtectionService protectionService)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
        _cachedPermissionService = cachedPermissionService;
        _protectionService = protectionService;
    }

    /// <summary>
    /// Debug endpoint to check current user's authorization details.
    /// </summary>
    /// <returns>Authorization debug information.</returns>
    [HttpGet("debug")]
    public async Task<ActionResult<AuthorizationDebugInfo>> GetAuthorizationDebugInfo()
    {
        var user = HttpContext.User;
        var providerSubjectId = user.FindFirstValue("oid") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        
        var userRoles = await _db.UserRoles
            .Where(ur => ur.ProviderSubjectId == providerSubjectId)
            .ToListAsync();

        var roleIds = userRoles.Select(ur => ur.RoleId).ToList();
        var roles = await _db.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .ToListAsync();

        var debugRoles = new List<UserRoleDebugInfo>();
        foreach (var ur in userRoles)
        {
            var role = roles.FirstOrDefault(r => r.Id == ur.RoleId);
            var permissions = new List<PermissionDebugInfo>();
            if (role != null)
            {
                permissions = role.RolePermissions.Select(rp => new PermissionDebugInfo
                {
                    Key = rp.Permission.Key,
                    DisplayName = rp.Permission.DisplayName,
                    IsActive = rp.Permission.IsActive
                }).ToList();
            }

            debugRoles.Add(new UserRoleDebugInfo
            {
                RoleAssignment = new UserRoleAssignmentDebugInfo
                {
                    Id = ur.Id,
                    RoleId = ur.RoleId,
                    IsFromEntra = ur.IsFromEntra,
                    AssignedUtc = ur.AssignedUtc
                },
                Role = role != null ? new RoleDebugInfo
                {
                    Id = role.Id,
                    Name = role.Name,
                    Description = role.Description,
                    IsActive = role.IsActive
                } : null,
                PermissionCount = permissions.Count,
                Permissions = permissions
            });
        }

        var debugInfo = new AuthorizationDebugInfo
        {
            IsAuthenticated = user.Identity?.IsAuthenticated,
            ProviderSubjectId = providerSubjectId,
            Claims = user.Claims.Select(c => new ClaimInfo { Type = c.Type, Value = c.Value }).ToList(),
            UserRoleCount = userRoles.Count,
            UserRoles = debugRoles
        };

        return Ok(debugInfo);
    }

    /// <summary>
    /// Lists all permissions configured in the system.
    /// </summary>
    /// <returns>A list of <see cref="PermissionInfo"/> items.</returns>
    [HttpGet("permissions")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<ActionResult<List<PermissionInfo>>> ListPermissions()
    {
        _logger.LogInformation("ListPermissions endpoint called");
        var permissions = await _db.Permissions.OrderBy(p => p.Key).ToListAsync();
        return Ok(_mapper.Map<List<PermissionInfo>>(permissions));
    }

    /// <summary>
    /// Lists all roles including their role-permission relations.
    /// </summary>
    /// <returns>A list of <see cref="RoleInfo"/> items.</returns>
    [HttpGet("roles")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<ActionResult<List<RoleInfo>>> ListRoles()
    {
        var roles = await _db.Roles.Include(r => r.RolePermissions).ToListAsync();
        return Ok(_mapper.Map<List<RoleInfo>>(roles));
    }

    /// <summary>
    /// Request payload to create or update a role.
    /// </summary>
    /// <param name="Id">The role identifier; null or empty to create a new role.</param>
    /// <param name="Name">The role display name.</param>
    /// <param name="Description">Optional description for the role.</param>
    /// <param name="EntraAppRoleId">Optional Entra App Role Id to bind/sync.</param>
    /// <param name="EntraAppRoleValue">Optional Entra App Role value/name to bind/sync.</param>
    public sealed record UpsertRoleRequest(Guid? Id, string Name, string? Description, Guid? EntraAppRoleId, string? EntraAppRoleValue);

    /// <summary>
    /// Creates or updates a role with protection rule validation.
    /// Invalidates all cached permissions if the role was updated.
    /// </summary>
    /// <param name="req">The upsert request.</param>
    /// <returns>The saved <see cref="RoleInfo"/>.</returns>
    [HttpPost("roles")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<ActionResult<RoleInfo>> UpsertRole([FromBody] UpsertRoleRequest req)
    {
        GreenlightRole role;
        var isUpdate = false;
        
        if (req.Id is Guid rid && rid != Guid.Empty)
        {
            role = await _db.Roles.FindAsync(rid) ?? throw new KeyNotFoundException("Role not found");
            
            // Validate the update against protection rules
            var validation = await _protectionService.ValidateRoleUpdateAsync(
                role.Id, req.EntraAppRoleValue, req.EntraAppRoleId);
            
            if (!validation.IsValid)
            {
                return BadRequest(validation.ErrorMessage);
            }
            
            role.Name = req.Name;
            role.Description = req.Description;
            role.EntraAppRoleId = req.EntraAppRoleId;
            role.EntraAppRoleValue = req.EntraAppRoleValue;
            isUpdate = true;
        }
        else
        {
            role = new GreenlightRole 
            { 
                Id = Guid.NewGuid(), 
                Name = req.Name, 
                Description = req.Description, 
                EntraAppRoleId = req.EntraAppRoleId,
                EntraAppRoleValue = req.EntraAppRoleValue
            };
            _db.Roles.Add(role);
        }
        
        await _db.SaveChangesAsync();
        
        if (isUpdate)
        {
            // Invalidate all cached permissions since role details changed
            await _cachedPermissionService.InvalidateAllPermissionsAsync();
            _logger.LogInformation("Role {RoleName} updated and cache invalidated", role.Name);
        }
        
        return Ok(_mapper.Map<RoleInfo>(role));
    }

    /// <summary>
    /// Deletes a role with protection rule validation.
    /// </summary>
    /// <param name="roleId">The role identifier to delete.</param>
    /// <returns>204 No Content if successful, or error response if validation fails.</returns>
    [HttpDelete("roles/{roleId:guid}")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<IActionResult> DeleteRole(Guid roleId)
    {
        // Validate the deletion against protection rules
        var validation = await _protectionService.ValidateRoleDeletionAsync(roleId);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ErrorMessage);
        }

        var role = await _db.Roles.FindAsync(roleId);
        if (role == null)
        {
            return NotFound();
        }

        // Remove all role permissions and user assignments
        var rolePermissions = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
        var userRoles = await _db.UserRoles.Where(ur => ur.RoleId == roleId).ToListAsync();

        _db.RolePermissions.RemoveRange(rolePermissions);
        _db.UserRoles.RemoveRange(userRoles);
        _db.Roles.Remove(role);

        await _db.SaveChangesAsync();

        // Invalidate all cached permissions since role was deleted
        await _cachedPermissionService.InvalidateAllPermissionsAsync();
        _logger.LogInformation("Role {RoleName} deleted and cache invalidated", role.Name);

        return NoContent();
    }

    /// <summary>
    /// Replaces the permissions assigned to a role with the provided set.
    /// Includes validation for protection rules.
    /// Invalidates cached permissions after changes.
    /// </summary>
    /// <param name="req">The request with target role and permission keys.</param>
    [HttpPost("roles/permissions")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<IActionResult> SetRolePermissions([FromBody] SetRolePermissionsRequest req)
    {
        var role = await _db.Roles.FindAsync(req.RoleId);
        if (role == null) return NotFound();
        
        var permissions = await _db.Permissions.Where(p => req.PermissionKeys.Contains(p.Key)).ToListAsync();
        var permissionIds = permissions.Select(p => p.Id).ToList();
        
        // Validate against protection rules
        var validation = await _protectionService.ValidateRolePermissionUpdateAsync(req.RoleId, permissionIds);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ErrorMessage);
        }
        
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync();
        
        _db.RolePermissions.RemoveRange(existing);
        foreach (var p in permissions)
        {
            _db.RolePermissions.Add(new GreenlightRolePermission { Id = Guid.NewGuid(), RoleId = role.Id, PermissionId = p.Id });
        }
        
        await _db.SaveChangesAsync();
        
        // Invalidate all cached permissions since role permissions changed
        await _cachedPermissionService.InvalidateAllPermissionsAsync();
        _logger.LogInformation("Role {RoleName} permissions updated and cache invalidated", role.Name);
        
        return NoContent();
    }

    /// <summary>
    /// Request payload to assign a role to a user.
    /// </summary>
    /// <param name="ProviderSubjectId">The user's provider subject identifier.</param>
    /// <param name="RoleId">The role identifier.</param>
    /// <param name="IsFromEntra">Whether the assignment originates from Entra App Role membership.</param>
    public sealed record AssignUserRoleRequest(string ProviderSubjectId, Guid RoleId, bool IsFromEntra = false);

    /// <summary>
    /// Assigns a role to a user if it is not already assigned.
    /// Invalidates the user's cached permissions after assignment.
    /// </summary>
    /// <param name="req">The assignment request.</param>
    /// <returns>204 No Content when done.</returns>
    [HttpPost("assignments")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<IActionResult> AssignUserRole([FromBody] AssignUserRoleRequest req)
    {
        var has = await _db.UserRoles.AnyAsync(ur => ur.ProviderSubjectId == req.ProviderSubjectId && ur.RoleId == req.RoleId);
        if (!has)
        {
            _db.UserRoles.Add(new GreenlightUserRole
            {
                Id = Guid.NewGuid(),
                ProviderSubjectId = req.ProviderSubjectId,
                RoleId = req.RoleId,
                IsFromEntra = req.IsFromEntra
            });
            await _db.SaveChangesAsync();
            
            // Invalidate the user's cached permissions
            await _cachedPermissionService.InvalidateUserPermissionsAsync(req.ProviderSubjectId);
            _logger.LogInformation("Role assigned to user {ProviderSubjectId} and cache invalidated", req.ProviderSubjectId);
        }
        return NoContent();
    }

    /// <summary>
    /// Removes a role assignment from a user with protection rule validation.
    /// Invalidates the user's cached permissions after removal.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <returns>204 No Content when removed, or 404 if not found.</returns>
    [HttpDelete("assignments/{providerSubjectId}/{roleId:guid}")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<IActionResult> RemoveAssignment(string providerSubjectId, Guid roleId)
    {
        // Validate against protection rules
        var validation = await _protectionService.ValidateUserRoleRemovalAsync(providerSubjectId, roleId);
        if (!validation.IsValid)
        {
            return BadRequest(validation.ErrorMessage);
        }

        var assignment = await _db.UserRoles.FirstOrDefaultAsync(ur => ur.ProviderSubjectId == providerSubjectId && ur.RoleId == roleId);
        if (assignment == null) return NotFound();
        
        _db.UserRoles.Remove(assignment);
        await _db.SaveChangesAsync();
        
        // Invalidate the user's cached permissions
        await _cachedPermissionService.InvalidateUserPermissionsAsync(providerSubjectId);
        _logger.LogInformation("Role assignment removed for user {ProviderSubjectId} and cache invalidated", providerSubjectId);
        
        return NoContent();
    }

    /// <summary>
    /// Searches users by full name or email and returns their current role assignments.
    /// </summary>
    /// <param name="q">The search query (min 2 characters).</param>
    /// <returns>A list of users matching the query with their role assignments.</returns>
    [HttpGet("users/search")]
    [RequiresPermission(PermissionKeys.ManageUsersAndRoles)]
    public async Task<ActionResult<List<UserSearchResult>>> SearchUsers([FromQuery] string q)
    {
        q = q?.Trim() ?? string.Empty;
        if (q.Length < 2)
        {
            return Ok(new List<UserSearchResult>());
        }

        // search by full name or email (case-insensitive)
        var users = await _db.UserInformations
            .AsNoTracking()
            .Where(u => (u.FullName != null && EF.Functions.Like(u.FullName, $"%{q}%"))
                     || (u.Email != null && EF.Functions.Like(u.Email, $"%{q}%")))
            .OrderBy(u => u.FullName)
            .Take(50)
            .Select(u => new { u.ProviderSubjectId, u.FullName, u.Email })
            .ToListAsync();

        var subs = users.Select(u => u.ProviderSubjectId).ToList();
        var roles = await _db.Roles.AsNoTracking().ToListAsync();
        var assignments = await _db.UserRoles
            .AsNoTracking()
            .Where(ur => subs.Contains(ur.ProviderSubjectId))
            .ToListAsync();

        var results = new List<UserSearchResult>();
        foreach (var u in users)
        {
            var userAssignments = assignments.Where(a => a.ProviderSubjectId == u.ProviderSubjectId)
                .Select(a => new UserSearchRoleAssignment
                {
                    RoleId = a.RoleId,
                    RoleName = roles.FirstOrDefault(r => r.Id == a.RoleId)?.Name ?? "",
                    IsFromEntra = a.IsFromEntra
                }).ToList();

            results.Add(new UserSearchResult
            {
                ProviderSubjectId = u.ProviderSubjectId,
                FullName = u.FullName,
                Email = u.Email,
                Assignments = userAssignments
            });
        }

        return Ok(results);
    }
}
