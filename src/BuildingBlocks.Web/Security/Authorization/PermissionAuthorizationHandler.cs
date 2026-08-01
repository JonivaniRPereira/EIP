using Microsoft.AspNetCore.Authorization;

namespace EIP.BuildingBlocks.Security.Authorization;

/// <summary>Nega por padrão (docs/07-Seguranca.md §5.2): só concede se a claim "permissions" do
/// token (emitida no login/select-tenant/refresh a partir da membership ativa) contiver a
/// permissão exigida. Ausência de claim ou de match nunca é tratada como acesso implícito.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var permissionsClaim = context.User.FindFirst(EipClaimTypes.Permissions)?.Value;
        if (string.IsNullOrWhiteSpace(permissionsClaim))
        {
            return Task.CompletedTask;
        }

        var permissions = permissionsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (permissions.Contains(requirement.Permission, StringComparer.Ordinal))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
