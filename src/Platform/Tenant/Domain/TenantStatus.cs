namespace EIP.Platform.Tenant.Domain;

/// <summary>Ciclo de vida do tenant (docs/08-Multi-Tenant.md §4.1, §9).</summary>
public enum TenantStatus
{
    Provisioning,
    Active,
    Suspended,
    ReadOnly,
    Archived,
    Deleted,
}
