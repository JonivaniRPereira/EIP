using Microsoft.AspNetCore.Authorization;

namespace EIP.BuildingBlocks.Security.Authorization;

/// <summary>Uso: <c>[RequirePermission(TenantPermissions.TenantView)]</c>. Resolvido em runtime por
/// <see cref="PermissionAuthorizationPolicyProvider"/>.</summary>
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "permission:";

    public RequirePermissionAttribute(string permission)
    {
        Policy = PolicyPrefix + permission;
    }
}
