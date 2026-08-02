namespace EIP.Data.Warehouse.Domain;

/// <summary>
/// docs/09-Data-Warehouse.md §5.2/§6.1 — SCD Tipo 2: toda mudança de atributo descritivo
/// (nome/e-mail/localização/status) gera uma nova versão, preservando a leitura histórica.
/// <see cref="CustomerId"/> é a chave de negócio durável (o <c>Customer.Id</c> canônico) que
/// atravessa todas as versões; <see cref="CustomerKey"/> é a chave substituta de cada versão
/// individual (docs/09 §6.1: <c>EffectiveFrom, EffectiveTo, IsCurrent, BusinessKey, SurrogateKey</c>).
/// </summary>
public sealed class DimCustomer
{
    public int CustomerKey { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string? Email { get; private set; }

    public string? City { get; private set; }

    public string? StateOrRegion { get; private set; }

    public string? CountryCode { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public bool IsCurrent { get; private set; }

    private DimCustomer()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private DimCustomer(
        Guid tenantId,
        Guid customerId,
        string code,
        string name,
        string? email,
        string? city,
        string? stateOrRegion,
        string? countryCode,
        bool isActive,
        DateTimeOffset effectiveFrom)
    {
        TenantId = tenantId;
        CustomerId = customerId;
        Code = code;
        Name = name;
        Email = email;
        City = city;
        StateOrRegion = stateOrRegion;
        CountryCode = countryCode;
        IsActive = isActive;
        EffectiveFrom = effectiveFrom;
        IsCurrent = true;
    }

    public static DimCustomer CreateCurrentVersion(
        Guid tenantId,
        Guid customerId,
        string code,
        string name,
        string? email,
        string? city,
        string? stateOrRegion,
        string? countryCode,
        bool isActive,
        DateTimeOffset effectiveFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DimCustomer(tenantId, customerId, code, name, email, city, stateOrRegion, countryCode, isActive, effectiveFrom);
    }

    /// <summary>Os atributos descritivos que, ao mudar, exigem uma nova versão (SCD Tipo 2) em vez de
    /// sobrescrita — decisão documentada aqui, não espalhada pelo processo de carga.</summary>
    public bool HasDescriptiveChangeComparedTo(string name, string? email, string? city, string? stateOrRegion, string? countryCode, bool isActive) =>
        Name != name || Email != email || City != city || StateOrRegion != stateOrRegion || CountryCode != countryCode || IsActive != isActive;

    /// <summary>Fecha esta versão (docs/09 §6.1) quando uma nova versão é criada — nunca sobrescreve
    /// a linha existente.</summary>
    public void Expire(DateTimeOffset effectiveTo)
    {
        EffectiveTo = effectiveTo;
        IsCurrent = false;
    }
}
