using EIP.Platform.Identity.Domain;
using EIP.Shared.Contracts.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace EIP.Host;

public sealed class PlatformUsageRequirement : IAuthorizationRequirement;

/// <summary>Permissão global persistida nas claims do Identity e revalidada a cada requisição.
/// Nunca deriva de Membership, papel Owner, claim de tenant ou conteúdo fornecido no request.</summary>
public sealed class PlatformUsageAuthorizationHandler(UserManager<ApplicationUser> users, IWebHostEnvironment environment) : AuthorizationHandler<PlatformUsageRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PlatformUsageRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            return;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (!environment.IsDevelopment() && !context.User.FindAll("amr").Any(c => c.Value == "mfa"))
        {
            return;
        }

        if (user is null || (users.SupportsUserLockout && await users.IsLockedOutAsync(user)))
        {
            return;
        }

        var claims = await users.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == PlatformPermissions.ClaimType && c.Value == PlatformPermissions.TenantUsageRead))
        {
            context.Succeed(requirement);
        }
    }
}
