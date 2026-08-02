using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Tenant.Domain;

/// <summary>Empresa legal ou unidade empresarial de um tenant (docs/08-Multi-Tenant.md §4.3).
/// Toda linha desta tabela é protegida por RLS obrigatória (ADR-007) — nunca remover o filtro sem
/// atualizar também a policy de segurança do banco.</summary>
public sealed class Company : Entity<Guid>
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; }
    public string? TradeName { get; private set; }
    public string? TaxId { get; private set; }
    public string CountryCode { get; private set; }
    public string DefaultCurrency { get; private set; }
    public CompanyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Company(Guid id, Guid tenantId, string name, string countryCode, string defaultCurrency, string? taxId, string? tradeName)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        CountryCode = countryCode;
        DefaultCurrency = defaultCurrency;
        TaxId = taxId;
        TradeName = tradeName;
        Status = CompanyStatus.Active;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Company()
    {
        Name = string.Empty;
        CountryCode = string.Empty;
        DefaultCurrency = string.Empty;
    }

    /// <summary>
    /// <paramref name="countryCode"/> é obrigatório (ISO 3166-1 alpha-2, ex. "BR") — exigido pelo
    /// Modelo Canônico (docs/04-Modelo-Canonico.md §5.1, entidade <c>Company</c>) para que
    /// <c>DimCompany</c> (docs/09-Data-Warehouse.md §5.2) tenha o atributo desde a origem, sem
    /// precisar de um valor inventado ou de uma migration de dados depois.
    /// </summary>
    public static Company Create(Guid tenantId, string name, string defaultCurrency, string countryCode, string? taxId = null, string? tradeName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        return new Company(Guid.NewGuid(), tenantId, name, countryCode, defaultCurrency, taxId, tradeName);
    }
}
