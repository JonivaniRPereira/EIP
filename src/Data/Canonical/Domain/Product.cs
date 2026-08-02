namespace EIP.Data.Canonical.Domain;

/// <summary>docs/04-Modelo-Canonico.md §5.2.</summary>
public sealed class Product : CanonicalEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public ProductType ProductType { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? UnitOfMeasure { get; private set; }
    public bool IsActive { get; private set; }

    private Product(
        Guid id,
        CanonicalLineage lineage,
        string code,
        string name,
        ProductType productType,
        bool isActive,
        Guid? categoryId,
        string? unitOfMeasure)
        : base(id, lineage)
    {
        Code = code;
        Name = name;
        ProductType = productType;
        IsActive = isActive;
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure;
    }

    private Product()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public static Product Create(
        CanonicalLineage lineage,
        string code,
        string name,
        ProductType productType,
        bool isActive,
        Guid? categoryId = null,
        string? unitOfMeasure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Product(Guid.NewGuid(), lineage, code, name, productType, isActive, categoryId, unitOfMeasure);
    }

    /// <summary>Upsert idempotente (E3.4).</summary>
    public void ApplyUpdate(
        CanonicalLineage lineage,
        string name,
        ProductType productType,
        bool isActive,
        Guid? categoryId,
        string? unitOfMeasure)
    {
        Name = name;
        ProductType = productType;
        IsActive = isActive;
        CategoryId = categoryId;
        UnitOfMeasure = unitOfMeasure;
        RefreshLineage(lineage);
    }
}
