using Microsoft.AspNetCore.Authorization;

namespace Microsoft.Greenlight.API.Main.Authorization;

public sealed class RequiresPermissionRequirement(string permissionKey) : IAuthorizationRequirement
{
    public string PermissionKey { get; } = permissionKey;
}
