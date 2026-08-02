namespace EIP.Data.Canonical.Domain;

/// <summary>
/// docs/04-Modelo-Canonico.md §5.3 — documento de faturamento, fonte padrão de receita do MVP.
/// Referencia <see cref="Customer"/> e (opcionalmente) um pedido comercial pela chave técnica, nunca
/// por navegação EF Core. Valores monetários sempre positivos (docs/04 §6.1) — o sinal econômico é
/// dado pelo tipo de documento/status, nunca pelo valor armazenado.
/// </summary>
public sealed class SalesInvoice : CanonicalEntity
{
    public string InvoiceNumber { get; private set; }
    public string? Series { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid? SalesOrderId { get; private set; }
    public SalesInvoiceStatus Status { get; private set; }
    public string CurrencyCode { get; private set; }
    public decimal GrossAmount { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal? TaxAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public DateTimeOffset? CanceledAt { get; private set; }

    private SalesInvoice(
        Guid id,
        CanonicalLineage lineage,
        string invoiceNumber,
        DateOnly issueDate,
        Guid customerId,
        SalesInvoiceStatus status,
        string currencyCode,
        decimal grossAmount,
        decimal discountAmount,
        decimal netAmount,
        string? series,
        Guid? salesOrderId,
        decimal? taxAmount)
        : base(id, lineage)
    {
        InvoiceNumber = invoiceNumber;
        IssueDate = issueDate;
        CustomerId = customerId;
        Status = status;
        CurrencyCode = currencyCode;
        GrossAmount = grossAmount;
        DiscountAmount = discountAmount;
        NetAmount = netAmount;
        Series = series;
        SalesOrderId = salesOrderId;
        TaxAmount = taxAmount;
    }

    private SalesInvoice()
    {
        InvoiceNumber = string.Empty;
        CurrencyCode = string.Empty;
    }

    public static SalesInvoice Create(
        CanonicalLineage lineage,
        string invoiceNumber,
        DateOnly issueDate,
        Guid customerId,
        SalesInvoiceStatus status,
        string currencyCode,
        decimal grossAmount,
        decimal discountAmount,
        decimal netAmount,
        string? series = null,
        Guid? salesOrderId = null,
        decimal? taxAmount = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);
        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("CustomerId é obrigatório.", nameof(customerId));
        }

        return new SalesInvoice(Guid.NewGuid(), lineage, invoiceNumber, issueDate, customerId, status, currencyCode, grossAmount, discountAmount, netAmount, series, salesOrderId, taxAmount);
    }

    /// <summary>Cancelamento é registrado, nunca apaga o documento (docs/04 §6.1/§6.2 — status
    /// canônico + carimbo de tempo do evento, preservando o histórico original).</summary>
    public void Cancel(DateTimeOffset canceledAt)
    {
        Status = SalesInvoiceStatus.Canceled;
        CanceledAt = canceledAt;
    }

    /// <summary>Upsert idempotente (E3.4). <see cref="InvoiceNumber"/> não muda — é a chave de
    /// negócio (junto com o restante da linhagem) que identifica que é "o mesmo" documento.</summary>
    public void ApplyUpdate(
        CanonicalLineage lineage,
        DateOnly issueDate,
        Guid customerId,
        SalesInvoiceStatus status,
        string currencyCode,
        decimal grossAmount,
        decimal discountAmount,
        decimal netAmount,
        string? series,
        Guid? salesOrderId,
        decimal? taxAmount)
    {
        IssueDate = issueDate;
        CustomerId = customerId;
        Status = status;
        CurrencyCode = currencyCode;
        GrossAmount = grossAmount;
        DiscountAmount = discountAmount;
        NetAmount = netAmount;
        Series = series;
        SalesOrderId = salesOrderId;
        TaxAmount = taxAmount;
        RefreshLineage(lineage);
    }
}
