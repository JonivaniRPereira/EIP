namespace EIP.Data.Canonical.Domain;

/// <summary>docs/04-Modelo-Canonico.md §5.2. Hierarquia opcional via <see cref="ParentCategoryId"/>
/// — não obrigatória para o mapeamento inicial da fatia Comercial.</summary>
public sealed class ProductCategory : CanonicalEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public Guid? ParentCategoryId { get; private set; }

    private ProductCategory(Guid id, CanonicalLineage lineage, string code, string name, Guid? parentCategoryId)
        : base(id, lineage)
    {
        Code = code;
        Name = name;
        ParentCategoryId = parentCategoryId;
    }

    private ProductCategory()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    public static ProductCategory Create(CanonicalLineage lineage, string code, string name, Guid? parentCategoryId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new ProductCategory(Guid.NewGuid(), lineage, code, name, parentCategoryId);
    }
}
