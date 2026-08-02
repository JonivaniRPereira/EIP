namespace EIP.Data.Warehouse.Domain;

/// <summary>docs/09-Data-Warehouse.md §5.2. SCD Tipo 1 (docs/09 §6.1): país/moeda corporativa não
/// precisam de histórico analítico nesta fase — sobrescreve sempre.</summary>
public sealed class DimCompany
{
    public int CompanyKey { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CompanyId { get; private set; }

    public string Name { get; private set; }

    public string CountryCode { get; private set; }

    public string DefaultCurrency { get; private set; }

    public DateTimeOffset LoadedAt { get; private set; }

    private DimCompany()
    {
        Name = string.Empty;
        CountryCode = string.Empty;
        DefaultCurrency = string.Empty;
    }

    private DimCompany(Guid tenantId, Guid companyId, string name, string countryCode, string defaultCurrency)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        Name = name;
        CountryCode = countryCode;
        DefaultCurrency = defaultCurrency;
        LoadedAt = DateTimeOffset.UtcNow;
    }

    public static DimCompany Create(Guid tenantId, Guid companyId, string name, string countryCode, string defaultCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        return new DimCompany(tenantId, companyId, name, countryCode, defaultCurrency);
    }

    public void ApplyUpdate(string name, string countryCode, string defaultCurrency)
    {
        Name = name;
        CountryCode = countryCode;
        DefaultCurrency = defaultCurrency;
        LoadedAt = DateTimeOffset.UtcNow;
    }
}
