namespace EIP.Platform.Tenant.Domain;

/// <summary>Papel da membership dentro do tenant (docs/08-Multi-Tenant.md §4.2, §6). O mapeamento
/// papel→permissões vive em <c>EIP.Shared.Contracts.Tenancy.RolePermissions</c>, não aqui — o
/// domínio Identity precisa resolver permissões sem depender do domínio Tenant.</summary>
public enum MembershipRole
{
    Owner,
    Admin,
    Member,
}
