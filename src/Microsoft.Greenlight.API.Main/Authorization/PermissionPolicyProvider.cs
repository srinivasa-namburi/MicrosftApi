using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Microsoft.Greenlight.API.Main.Authorization;

/// <summary>
/// Builds policies on-demand for RequiresPermissionAttribute and RequiresAnyPermissionAttribute using the naming convention.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Handle single permission requirement
        if (policyName.StartsWith(RequiresPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permissionKey = policyName.Substring(RequiresPermissionAttribute.PolicyPrefix.Length);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new RequiresPermissionRequirement(permissionKey))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        
        // Handle multiple permission requirement (OR logic)
        if (policyName.StartsWith(RequiresAnyPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permissionKeysString = policyName.Substring(RequiresAnyPermissionAttribute.PolicyPrefix.Length);
            var permissionKeys = permissionKeysString.Split('|', StringSplitOptions.RemoveEmptyEntries);
            
            if (permissionKeys.Length > 0)
            {
                var policy = new AuthorizationPolicyBuilder()
                    .AddRequirements(new RequiresAnyPermissionRequirement(permissionKeys))
                    .Build();
                return Task.FromResult<AuthorizationPolicy?>(policy);
            }
        }
        
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
