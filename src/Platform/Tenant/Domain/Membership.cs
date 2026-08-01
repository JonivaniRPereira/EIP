using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Tenant.Domain;

/// <summary>Vínculo de um usuário (Identity) a um tenant (docs/08-Multi-Tenant.md §4.2). Referencia
/// o usuário apenas por <see cref="UserId"/> — nunca por navegação direta ao domínio Identity
/// (isolamento de persistência entre domínios, docs/02-Arquitetura.md §9.2).
/// Protegida por RLS obrigatória (ADR-007).</summary>
public sealed class Membership : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Membership(Guid id, Guid userId, Guid tenantId)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        Status = MembershipStatus.Invited;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Membership()
    {
    }

    public static Membership Create(Guid userId, Guid tenantId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId é obrigatório.", nameof(userId));
        }

        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        return new Membership(Guid.NewGuid(), userId, tenantId);
    }

    public void Activate()
    {
        Status = MembershipStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
