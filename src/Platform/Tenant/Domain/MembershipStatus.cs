namespace EIP.Platform.Tenant.Domain;

/// <summary>docs/08-Multi-Tenant.md §4.2 — vínculo do usuário ao tenant.</summary>
public enum MembershipStatus
{
    Invited,
    Active,
    Suspended,
    Removed,
}
