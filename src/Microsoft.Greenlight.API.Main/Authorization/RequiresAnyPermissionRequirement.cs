// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Authorization requirement that succeeds if the user has ANY of the specified permissions.
/// </summary>
public sealed class RequiresAnyPermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the list of permission keys, any one of which satisfies this requirement.
    /// </summary>
    public IReadOnlyList<string> PermissionKeys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresAnyPermissionRequirement"/> class.
    /// </summary>
    /// <param name="permissionKeys">The permission keys, any one of which satisfies this requirement.</param>
    public RequiresAnyPermissionRequirement(params string[] permissionKeys)
    {
        if (permissionKeys == null || permissionKeys.Length == 0)
        {
            throw new ArgumentException("At least one permission key must be specified", nameof(permissionKeys));
        }
        
        PermissionKeys = permissionKeys.ToList().AsReadOnly();
    }
}