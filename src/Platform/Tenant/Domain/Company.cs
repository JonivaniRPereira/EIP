using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Tenant.Domain;

/// <summary>Empresa legal ou unidade empresarial de um tenant (docs/08-Multi-Tenant.md §4.3).
/// Toda linha desta tabela é protegida por RLS obrigatória (ADR-007) — nunca remover o filtro sem
/// atualizar também a policy de segurança do banco.</summary>
public sealed class Company : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? TaxId { get; private set; }
    public string DefaultCurrency { get; private set; }
    public CompanyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Company(Guid id, Guid tenantId, string name, string defaultCurrency, string? taxId)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        DefaultCurrency = defaultCurrency;
        TaxId = taxId;
        Status = CompanyStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Company()
    {
        Name = string.Empty;
        DefaultCurrency = string.Empty;
    }

    public static Company Create(Guid tenantId, string name, string defaultCurrency, string? taxId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        return new Company(Guid.NewGuid(), tenantId, name, defaultCurrency, taxId);
    }
}
