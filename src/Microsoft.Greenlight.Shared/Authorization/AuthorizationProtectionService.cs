// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.EntityFrameworkCore;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Models.Authorization;
using Microsoft.Greenlight.Shared.Contracts.Authorization;
using Microsoft.Extensions.Logging;

namespace Microsoft.Greenlight.Shared.Authorization;

/// <summary>
/// Service that enforces authorization protection rules to ensure system integrity.
/// Prevents scenarios that could lead to system lockout or security vulnerabilities.
/// </summary>
public sealed class AuthorizationProtectionService
{
    private readonly IDbContextFactory<DocGenerationDbContext> _dbContextFactory;
    private readonly ILogger<AuthorizationProtectionService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationProtectionService"/> class.
    /// </summary>
    /// <param name="dbContextFactory">Database context factory for data access.</param>
    /// <param name="logger">Logger instance for recording protection rule violations.</param>
    public AuthorizationProtectionService(IDbContextFactory<DocGenerationDbContext> dbContextFactory, ILogger<AuthorizationProtectionService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Validation result for authorization operations.
    /// </summary>
    /// <param name="IsValid">Whether the operation is valid.</param>
    /// <param name="ErrorMessage">Error message if the operation is not valid.</param>
    public sealed record ValidationResult(bool IsValid, string? ErrorMessage = null)
    {
        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static ValidationResult Success() => new(true);

        /// <summary>
        /// Creates a failed validation result with an error message.
        /// </summary>
        /// <param name="errorMessage">The error message describing why validation failed.</param>
        public static ValidationResult Failure(string errorMessage) => new(false, errorMessage);
    }

    /// <summary>
    /// Validates whether a role can be updated with the specified Entra App Role mapping.
    /// </summary>
    /// <param name="roleId">The role identifier being updated.</param>
    /// <param name="newEntraAppRoleValue">The new Entra App Role value.</param>
    /// <param name="newEntraAppRoleId">The new Entra App Role ID.</param>
    /// <returns>Validation result indicating if the update is allowed.</returns>
    public async Task<ValidationResult> ValidateRoleUpdateAsync(Guid roleId, string? newEntraAppRoleValue, Guid? newEntraAppRoleId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Protection Rule: Cannot remove DocumentGeneration mapping if it's the only one
        var currentRole = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (currentRole == null)
        {
            return ValidationResult.Failure("Role not found");
        }

        // Check if this role currently has DocumentGeneration mapping
        var currentlyMappedToDocumentGeneration = !string.IsNullOrEmpty(currentRole.EntraAppRoleValue) &&
            string.Equals(currentRole.EntraAppRoleValue, "DocumentGeneration", StringComparison.OrdinalIgnoreCase);

        // Check if the new mapping removes DocumentGeneration
        var newMappingRemovesDocumentGeneration = currentlyMappedToDocumentGeneration &&
            (string.IsNullOrEmpty(newEntraAppRoleValue) ||
             !string.Equals(newEntraAppRoleValue, "DocumentGeneration", StringComparison.OrdinalIgnoreCase));

        if (newMappingRemovesDocumentGeneration)
        {
            // Check if there are other roles with DocumentGeneration mapping
            var otherDocumentGenerationRoles = await db.Roles
                .Where(r => r.Id != roleId &&
                           r.IsActive &&
                           r.EntraAppRoleValue != null &&
                           (r.EntraAppRoleValue == "DocumentGeneration" ||
                            r.EntraAppRoleValue.ToLower() == "documentgeneration"))
                .AnyAsync();

            if (!otherDocumentGenerationRoles)
            {
                return ValidationResult.Failure("Cannot remove DocumentGeneration Entra App Role mapping from this role as it's the only role mapped to DocumentGeneration. At least one active role must be mapped to DocumentGeneration to ensure system access.");
            }
        }

        // Protection Rule: Cannot disable the FullAccess role
        if (roleId == AuthorizationIds.Roles.FullAccess)
        {
            return ValidationResult.Failure("The FullAccess role cannot be modified through this interface as it's a system-protected role.");
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates whether a role can be deleted.
    /// </summary>
    /// <param name="roleId">The role identifier to delete.</param>
    /// <returns>Validation result indicating if the deletion is allowed.</returns>
    public async Task<ValidationResult> ValidateRoleDeletionAsync(Guid roleId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Protection Rule: Cannot delete the FullAccess role
        if (roleId == AuthorizationIds.Roles.FullAccess)
        {
            return ValidationResult.Failure("The FullAccess role cannot be deleted as it's a system-protected role required for platform administration.");
        }

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId);
        if (role == null)
        {
            return ValidationResult.Success(); // Already deleted
        }

        // Protection Rule: Cannot delete the last role mapped to DocumentGeneration
        var isDocumentGenerationRole = !string.IsNullOrEmpty(role.EntraAppRoleValue) &&
            string.Equals(role.EntraAppRoleValue, "DocumentGeneration", StringComparison.OrdinalIgnoreCase);

        if (isDocumentGenerationRole)
        {
            var otherDocumentGenerationRoles = await db.Roles
                .Where(r => r.Id != roleId &&
                           r.IsActive &&
                           r.EntraAppRoleValue != null &&
                           (r.EntraAppRoleValue == "DocumentGeneration" ||
                            r.EntraAppRoleValue.ToLower() == "documentgeneration"))
                .AnyAsync();

            if (!otherDocumentGenerationRoles)
            {
                return ValidationResult.Failure("Cannot delete this role as it's the only role mapped to the DocumentGeneration Entra App Role. At least one active role must be mapped to DocumentGeneration to ensure system access.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates whether permissions can be updated for a role.
    /// </summary>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="newPermissionIds">The new set of permission IDs to assign to the role.</param>
    /// <returns>Validation result indicating if the permission update is allowed.</returns>
    public async Task<ValidationResult> ValidateRolePermissionUpdateAsync(Guid roleId, List<Guid> newPermissionIds)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Protection Rule: FullAccess role must have all permissions
        if (roleId == AuthorizationIds.Roles.FullAccess)
        {
            var allPermissionIds = await db.Permissions.Select(p => p.Id).ToListAsync();
            var missingPermissions = allPermissionIds.Except(newPermissionIds).ToList();

            if (missingPermissions.Count > 0)
            {
                return ValidationResult.Failure($"The FullAccess role must have all permissions. Missing {missingPermissions.Count} permission(s). This role cannot have its permissions reduced as it's a system-protected role.");
            }
        }

        return ValidationResult.Success();
    }

    /// <summary>
    /// Validates whether a user role assignment can be removed.
    /// </summary>
    /// <param name="providerSubjectId">The user's provider subject identifier.</param>
    /// <param name="roleId">The role identifier being removed.</param>
    /// <returns>Validation result indicating if the removal is allowed.</returns>
    public async Task<ValidationResult> ValidateUserRoleRemovalAsync(string providerSubjectId, Guid roleId)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync();

        // Protection Rule: At least one user must always have FullAccess role
        if (roleId == AuthorizationIds.Roles.FullAccess)
        {
            var otherFullAccessUsers = await db.UserRoles
                .Where(ur => ur.ProviderSubjectId != providerSubjectId &&
                            ur.RoleId == AuthorizationIds.Roles.FullAccess)
                .AnyAsync();

            if (!otherFullAccessUsers)
            {
                // Check if this user has FullAccess through other roles (shouldn't happen with current design, but defensive)
                var userHasFullAccessThroughOtherRoles = await db.UserRoles
                    .Where(ur => ur.ProviderSubjectId == providerSubjectId &&
                                ur.RoleId != roleId)
                    .Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r)
                    .Where(r => r.IsActive)
                    .AnyAsync();

                if (!userHasFullAccessThroughOtherRoles)
                {
                    return ValidationResult.Failure("Cannot remove FullAccess role from this user as they are the last user with FullAccess permissions. At least one user must always have FullAccess to prevent system lockout.");
                }
            }
        }

        return ValidationResult.Success();
    }
}