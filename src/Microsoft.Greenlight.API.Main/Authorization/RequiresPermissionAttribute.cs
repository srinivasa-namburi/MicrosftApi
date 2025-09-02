using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Declaratively requires a Greenlight permission for a controller action.
/// </summary>
public sealed class RequiresPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public RequiresPermissionAttribute(string permissionKey)
    {
        Policy = PolicyPrefix + permissionKey;
    }
}
