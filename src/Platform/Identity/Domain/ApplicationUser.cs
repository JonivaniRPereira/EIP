using Microsoft.AspNetCore.Identity;

namespace EIP.Platform.Identity.Domain;

/// <summary>
/// Identidade que pode pertencer a um ou mais tenants (docs/08-Multi-Tenant.md §2). O usuário em si
/// não carrega TenantId — o vínculo é a Membership, no domínio Tenant.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
