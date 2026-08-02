namespace EIP.Data.Warehouse.Domain;

/// <summary>docs/09-Data-Warehouse.md §5.2 — usada para governança/join, nunca para expor dados de
/// outro tenant. Protegida por RLS obrigatória (ADR-007) como qualquer outra tabela deste schema,
/// mesmo que na prática um tenant só tenha uma única linha própria aqui.</summary>
public sealed class DimTenant
{
    public int TenantKey { get; private set; }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public DateTimeOffset LoadedAt { get; private set; }

    private DimTenant()
    {
        Name = string.Empty;
    }

    private DimTenant(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
        LoadedAt = DateTimeOffset.UtcNow;
    }

    public static DimTenant Create(Guid tenantId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DimTenant(tenantId, name);
    }

    /// <summary>SCD Tipo 1 (docs/09 §6.1): atributos de governança não têm interpretação histórica
    /// analítica relevante — sobrescreve sempre.</summary>
    public void ApplyUpdate(string name)
    {
        Name = name;
        LoadedAt = DateTimeOffset.UtcNow;
    }
}
