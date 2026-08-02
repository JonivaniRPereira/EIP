namespace EIP.Data.Warehouse.Domain;

/// <summary>docs/09-Data-Warehouse.md §5.2 — "permite análise de mix". Modelada para completude do
/// esquema dimensional, mas nunca populada nesta fase: o conector de referência não ingere
/// categorias de produto (mesma lacuna já documentada em <c>EIP.Data.Canonical.Domain.Product.
/// CategoryId</c>, sempre nulo). <see cref="DimProduct.CategoryKey"/> permanece nulo até que uma
/// fonte real de categorias exista.</summary>
public sealed class DimProductCategory
{
    public int ProductCategoryKey { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid CategoryId { get; private set; }

    public string Name { get; private set; }

    private DimProductCategory()
    {
        Name = string.Empty;
    }

    private DimProductCategory(Guid tenantId, Guid categoryId, string name)
    {
        TenantId = tenantId;
        CategoryId = categoryId;
        Name = name;
    }

    public static DimProductCategory Create(Guid tenantId, Guid categoryId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DimProductCategory(tenantId, categoryId, name);
    }
}
