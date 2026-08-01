namespace EIP.Shared.Contracts.Tenancy;

/// <summary><paramref name="Role"/> é o nome do <c>MembershipRole</c> do domínio Tenant, como
/// string — este tipo vive em Shared e não pode depender do domínio Tenant (docs/02 §9.2), então a
/// conversão papel→permissões usa nomes de string (ver <see cref="RolePermissions"/>).</summary>
public sealed record MembershipSummary(Guid TenantId, string TenantName, string TenantSlug, string Role);
