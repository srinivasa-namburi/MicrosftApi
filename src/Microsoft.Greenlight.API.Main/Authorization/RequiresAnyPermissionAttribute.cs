// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Declaratively requires ANY of the specified Greenlight permissions for a controller action.
/// Uses OR logic - the user needs to have at least one of the specified permissions.
/// </summary>
public sealed class RequiresAnyPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "AnyPermission:";

    public RequiresAnyPermissionAttribute(params string[] permissionKeys)
    {
        if (permissionKeys == null || permissionKeys.Length == 0)
        {
            throw new ArgumentException("At least one permission key must be specified", nameof(permissionKeys));
        }
        
        // Create a unique policy name based on the combination of permission keys
        var sortedKeys = permissionKeys.OrderBy(k => k).ToArray();
        Policy = PolicyPrefix + string.Join("|", sortedKeys);
    }
}