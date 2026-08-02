namespace EIP.Data.Canonical.Domain;

/// <summary>docs/04-Modelo-Canonico.md §5.3 — granularidade preferida para análises de produto/
/// categoria/margem (também o grão do fato <c>FactSalesInvoiceItem</c> do Data Warehouse,
/// docs/09-Data-Warehouse.md §5.3).</summary>
public sealed class SalesInvoiceItem : CanonicalEntity
{
    public Guid SalesInvoiceId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid ProductId { get; private set; }
    public string? Description { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal? TaxAmount { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal NetAmount { get; private set; }

    private SalesInvoiceItem(
        Guid id,
        CanonicalLineage lineage,
        Guid salesInvoiceId,
        int lineNumber,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal grossAmount,
        decimal netAmount,
        string? description,
        decimal? taxAmount)
        : base(id, lineage)
    {
        SalesInvoiceId = salesInvoiceId;
        LineNumber = lineNumber;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount;
        GrossAmount = grossAmount;
        NetAmount = netAmount;
        Description = description;
        TaxAmount = taxAmount;
    }

    private SalesInvoiceItem()
    {
    }

    public static SalesInvoiceItem Create(
        CanonicalLineage lineage,
        Guid salesInvoiceId,
        int lineNumber,
        Guid productId,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        decimal grossAmount,
        decimal netAmount,
        string? description = null,
        decimal? taxAmount = null)
    {
        if (salesInvoiceId == Guid.Empty)
        {
            throw new ArgumentException("SalesInvoiceId é obrigatório.", nameof(salesInvoiceId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId é obrigatório.", nameof(productId));
        }

        return new SalesInvoiceItem(Guid.NewGuid(), lineage, salesInvoiceId, lineNumber, productId, quantity, unitPrice, discountAmount, grossAmount, netAmount, description, taxAmount);
    }
}
