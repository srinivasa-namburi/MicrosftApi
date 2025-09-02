// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Authorization;

/// <summary>
/// Seeds default permissions, roles, and bootstrap role assignments.
/// Handles default Entra App Role mappings and protection rules.
/// </summary>
public sealed class AuthorizationSeeder
{
    private readonly DocGenerationDbContext _db;
    private readonly ILogger<AuthorizationSeeder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationSeeder"/> class.
    /// </summary>
    public AuthorizationSeeder(DocGenerationDbContext db, ILogger<AuthorizationSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Ensures permissions, roles, and default assignments are present and up to date.
    /// </summary>
    public async Task EnsureSeededAsync(CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Seed permissions
        var wanted = new[]
        {
            new GreenlightPermission { Id = AuthorizationIds.Permissions.AlterSystemConfiguration, Key = PermissionKeys.AlterSystemConfiguration, DisplayName = "Alter system configuration", Description = "Change global platform options, import/export indexes, reindex, and system-level operations." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.ManageLlmModelsAndDeployments, Key = PermissionKeys.ManageLlmModelsAndDeployments, DisplayName = "Manage LLM models and deployments", Description = "Create, update, or remove model connections and LLM deployment settings." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.GenerateDocument, Key = PermissionKeys.GenerateDocument, DisplayName = "Generate Document", Description = "Generate documents, upload inputs, and download outputs." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.ManageUsersAndRoles, Key = PermissionKeys.ManageUsersAndRoles, DisplayName = "Manage Users and Roles", Description = "Create and edit roles, assign permissions, and manage user role assignments." },
            
            // Added 09/01/25
            new GreenlightPermission { Id = AuthorizationIds.Permissions.Chat, Key = PermissionKeys.Chat, DisplayName = "Chat", Description = "Use chat capabilities." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.DefineReviews, Key = PermissionKeys.DefineReviews, DisplayName = "Define Reviews", Description = "Create and manage review definitions and templates." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.ExecuteReviews, Key = PermissionKeys.ExecuteReviews, DisplayName = "Execute Reviews", Description = "Perform reviews and access review results." },
            new GreenlightPermission { Id = AuthorizationIds.Permissions.AlterDocumentProcessesAndLibraries, Key = PermissionKeys.AlterDocumentProcessesAndLibraries, DisplayName = "Alter Document Processes and Libraries", Description = "Create and manage document processing workflows and document libraries." },
        };

        foreach (var p in wanted)
        {
            var existing = await _db.Permissions.FirstOrDefaultAsync(x => x.Key == p.Key, ct);
            if (existing == null)
            {
                _db.Permissions.Add(p);
            }
            else
            {
                // Update display metadata if changed
                var changed = false;
                if (!string.Equals(existing.DisplayName, p.DisplayName, StringComparison.Ordinal)) { existing.DisplayName = p.DisplayName; changed = true; }
                if (!string.Equals(existing.Description, p.Description, StringComparison.Ordinal)) { existing.Description = p.Description; changed = true; }
                if (changed) _db.Permissions.Update(existing);
            }
        }

        // Create a built-in role that aggregates all permissions ("FullAccess")
        // Prefer lookup by fixed ID, then fallback to name for first-time migration.
        var fullRole = await _db.Roles.FirstOrDefaultAsync(r => r.Id == AuthorizationIds.Roles.FullAccess, ct)
            ?? await _db.Roles.FirstOrDefaultAsync(r => r.Name == "FullAccess", ct);
        if (fullRole == null)
        {
            fullRole = new GreenlightRole
            {
                Id = AuthorizationIds.Roles.FullAccess,
                Name = "FullAccess",
                Description = "Built-in role with all permissions",
                // Default mapping to DocumentGeneration Entra App Role
                EntraAppRoleValue = "DocumentGeneration"
            };
            _db.Roles.Add(fullRole);
            _logger.LogInformation("Created FullAccess role with default DocumentGeneration Entra App Role mapping");
        }
        else if (fullRole.Id != AuthorizationIds.Roles.FullAccess)
        {
            // If discovered by name but with a different ID, normalize to fixed ID for determinism.
            fullRole.Id = AuthorizationIds.Roles.FullAccess;
            _db.Roles.Update(fullRole);
        }

        // Apply default Entra App Role mapping if none exists and no custom mapping was set
        if (fullRole != null && string.IsNullOrEmpty(fullRole.EntraAppRoleValue) && !fullRole.EntraAppRoleId.HasValue)
        {
            // Only set default mapping if the role hasn't been customized
            var hasCustomPermissions = await _db.RolePermissions
                .Where(rp => rp.RoleId == fullRole.Id)
                .CountAsync(ct);

            // If this is a fresh installation or the role has all permissions (indicating it's still the default)
            var allPermissionCount = await _db.Permissions.CountAsync(ct);
            if (hasCustomPermissions == 0 || hasCustomPermissions == allPermissionCount)
            {
                fullRole.EntraAppRoleValue = "DocumentGeneration";
                _db.Roles.Update(fullRole);
                _logger.LogInformation("Applied default DocumentGeneration Entra App Role mapping to FullAccess role");
            }
        }

        await _db.SaveChangesAsync(ct);

        // Ensure FullAccess role has all permissions (Protection Rule #3)
        await EnsureFullAccessRoleHasAllPermissionsAsync(fullRole, ct);

        await _db.SaveChangesAsync(ct);

        // Bootstrap: assign FullAccess to all existing users if they have no roles yet (Not from Entra)
        var users = await _db.UserInformations.Select(u => u.ProviderSubjectId).ToListAsync(ct);
        var existingAssignments = await _db.UserRoles.Where(ur => users.Contains(ur.ProviderSubjectId)).ToListAsync(ct);
        foreach (var userSub in users)
        {
            if (!existingAssignments.Any(a => a.ProviderSubjectId == userSub))
            {
                _db.UserRoles.Add(new GreenlightUserRole
                {
                    Id = Guid.NewGuid(),
                    ProviderSubjectId = userSub,
                    RoleId = fullRole.Id,
                    IsFromEntra = false
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        // Ensure protection rules are enforced
        await EnforceProtectionRulesAsync(ct);

        await tx.CommitAsync(ct);
    }

    /// <summary>
    /// Ensures the FullAccess role has all permissions (Protection Rule #3).
    /// </summary>
    private async Task EnsureFullAccessRoleHasAllPermissionsAsync(GreenlightRole fullRole, CancellationToken ct)
    {
        var allPermissionIds = await _db.Permissions.Select(p => p.Id).ToListAsync(ct);
        var existingRolePerms = await _db.RolePermissions.Where(rp => rp.RoleId == fullRole.Id).ToListAsync(ct);
        
        foreach (var permissionId in allPermissionIds)
        {
            if (!existingRolePerms.Any(x => x.PermissionId == permissionId))
            {
                _db.RolePermissions.Add(new GreenlightRolePermission 
                { 
                    Id = Guid.NewGuid(), 
                    RoleId = fullRole.Id, 
                    PermissionId = permissionId 
                });
            }
        }
    }

    /// <summary>
    /// Enforces the critical protection rules for the authorization system.
    /// </summary>
    private async Task EnforceProtectionRulesAsync(CancellationToken ct)
    {
        // Protection Rule #1: DocumentGeneration Entra App Role must be mapped to some role
        await EnsureDocumentGenerationRoleMappingAsync(ct);

        // Protection Rule #2: At least one user must have FullAccess role
        await EnsureAtLeastOneFullAccessUserAsync(ct);
    }

    /// <summary>
    /// Ensures that the DocumentGeneration Entra App Role is mapped to at least one active role.
    /// If not, maps it to FullAccess role as fallback (Protection Rule #1).
    /// </summary>
    private async Task EnsureDocumentGenerationRoleMappingAsync(CancellationToken ct)
    {
        // Use EF-translatable operations instead of StringComparison
        var documentGenerationMapped = await _db.Roles
            .Where(r => r.IsActive && 
                       r.EntraAppRoleValue != null &&
                       (r.EntraAppRoleValue == "DocumentGeneration" || 
                        r.EntraAppRoleValue.ToLower() == "documentgeneration"))
            .AnyAsync(ct);

        if (!documentGenerationMapped)
        {
            _logger.LogWarning("DocumentGeneration Entra App Role is not mapped to any role. Mapping to FullAccess role as fallback.");
            
            var fullAccessRole = await _db.Roles.FirstAsync(r => r.Id == AuthorizationIds.Roles.FullAccess, ct);
            fullAccessRole.EntraAppRoleValue = "DocumentGeneration";
            _db.Roles.Update(fullAccessRole);
            
            _logger.LogInformation("Mapped DocumentGeneration Entra App Role to FullAccess role to ensure system access");
        }
    }

    /// <summary>
    /// Ensures that at least one user has the FullAccess role assigned.
    /// If not, assigns it to the first user in the system (Protection Rule #2).
    /// </summary>
    private async Task EnsureAtLeastOneFullAccessUserAsync(CancellationToken ct)
    {
        var hasFullAccessUser = await _db.UserRoles
            .Where(ur => ur.RoleId == AuthorizationIds.Roles.FullAccess)
            .AnyAsync(ct);

        if (!hasFullAccessUser)
        {
            var firstUser = await _db.UserInformations
                .Select(u => u.ProviderSubjectId)
                .FirstOrDefaultAsync(ct);

            if (firstUser != null)
            {
                _db.UserRoles.Add(new GreenlightUserRole
                {
                    Id = Guid.NewGuid(),
                    ProviderSubjectId = firstUser,
                    RoleId = AuthorizationIds.Roles.FullAccess,
                    IsFromEntra = false
                });

                _logger.LogWarning("No users had FullAccess role. Assigned FullAccess role to user {ProviderSubjectId} to ensure system access", firstUser);
            }
        }
    }
}
