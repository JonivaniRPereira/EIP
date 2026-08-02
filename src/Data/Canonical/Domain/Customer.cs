namespace EIP.Data.Canonical.Domain;

/// <summary>docs/04-Modelo-Canonico.md §5.2. Cadastro de apoio da fatia Comercial — referenciado por
/// <see cref="SalesInvoice"/> pela chave técnica (<see cref="Entity{TId}.Id"/>), nunca por
/// navegação EF Core (mesmo princípio de isolamento usado entre Membership/Tenant, docs/02 §9.2:
/// aqui não é cross-domain, mas evita acoplar constraints de FK a tabelas com RLS).</summary>
public sealed class Customer : CanonicalEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public string? TaxId { get; private set; }
    public string? Email { get; private set; }
    public string? City { get; private set; }
    public string? StateOrRegion { get; private set; }
    public string? CountryCode { get; private set; }
    public bool IsActive { get; private set; }

    private Customer(
        Guid id,
        CanonicalLineage lineage,
        string code,
        string name,
        bool isActive,
        string? taxId,
        string? email,
        string? city,
        string? stateOrRegion,
        string? countryCode)
        : base(id, lineage)
    {
        Code = code;
        Name = name;
        IsActive = isActive;
        TaxId = taxId;
        Email = email;
        City = city;
        StateOrRegion = stateOrRegion;
        CountryCode = countryCode;
    }

    private Customer()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public static Customer Create(
        CanonicalLineage lineage,
        string code,
        string name,
        bool isActive,
        string? taxId = null,
        string? email = null,
        string? city = null,
        string? stateOrRegion = null,
        string? countryCode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Customer(Guid.NewGuid(), lineage, code, name, isActive, taxId, email, city, stateOrRegion, countryCode);
    }
}
