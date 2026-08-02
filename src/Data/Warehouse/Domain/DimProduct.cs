namespace EIP.Data.Warehouse.Domain;

/// <summary>docs/09-Data-Warehouse.md §5.2/§6.1 — SCD Tipo 2, mesmo raciocínio de
/// <see cref="DimCustomer"/>. <see cref="ProductId"/> é a chave de negócio durável (o
/// <c>Product.Id</c> canônico); <see cref="ProductKey"/> é a chave substituta de cada versão.
/// <see cref="CategoryKey"/> fica sempre nulo nesta fase (ver <see cref="DimProductCategory"/>).</summary>
public sealed class DimProduct
{
    public int ProductKey { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    public string ProductType { get; private set; }

    public int? CategoryKey { get; private set; }

    public string? UnitOfMeasure { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset EffectiveFrom { get; private set; }

    public DateTimeOffset? EffectiveTo { get; private set; }

    public bool IsCurrent { get; private set; }

    private DimProduct()
    {
        Code = string.Empty;
        Name = string.Empty;
        ProductType = string.Empty;
    }

    private DimProduct(
        Guid tenantId,
        Guid productId,
        string code,
        string name,
        string productType,
        int? categoryKey,
        string? unitOfMeasure,
        bool isActive,
        DateTimeOffset effectiveFrom)
    {
        TenantId = tenantId;
        ProductId = productId;
        Code = code;
        Name = name;
        ProductType = productType;
        CategoryKey = categoryKey;
        UnitOfMeasure = unitOfMeasure;
        IsActive = isActive;
        EffectiveFrom = effectiveFrom;
        IsCurrent = true;
    }

    public static DimProduct CreateCurrentVersion(
        Guid tenantId,
        Guid productId,
        string code,
        string name,
        string productType,
        int? categoryKey,
        string? unitOfMeasure,
        bool isActive,
        DateTimeOffset effectiveFrom)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(productType);
        return new DimProduct(tenantId, productId, code, name, productType, categoryKey, unitOfMeasure, isActive, effectiveFrom);
    }

    public bool HasDescriptiveChangeComparedTo(string name, string productType, int? categoryKey, string? unitOfMeasure, bool isActive) =>
        Name != name || ProductType != productType || CategoryKey != categoryKey || UnitOfMeasure != unitOfMeasure || IsActive != isActive;

    public void Expire(DateTimeOffset effectiveTo)
    {
        EffectiveTo = effectiveTo;
        IsCurrent = false;
    }
}
