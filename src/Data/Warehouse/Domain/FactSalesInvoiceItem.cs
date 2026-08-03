namespace EIP.Data.Warehouse.Domain;

/// <summary>
/// docs/09-Data-Warehouse.md §5.3 — grão imutável: uma linha por item de fatura emitida. Custo/
/// margem ficam de fora (não disponíveis na origem do conector de referência, docs/roadmap/
/// fase-1-backlog.md E5.2). A chave de negócio para upsert idempotente é
/// <c>(TenantId, SourceSystemId, SourceEntity, SourceRecordId)</c> — a mesma linhagem do CDM
/// (docs/04 §4.1), nunca <see cref="SalesInvoiceItemId"/>: o Modelo Canônico substitui (delete+insert)
/// os itens de uma fatura a cada reprocessamento (E3.4), então o Guid do item não é estável entre
/// cargas — só o <c>SourceRecordId</c> (linha de origem) é.
/// </summary>
public sealed class FactSalesInvoiceItem
{
    public int FactSalesInvoiceItemKey { get; private set; }

    public Guid TenantId { get; private set; }

    public int TenantKey { get; private set; }

    public int CompanyKey { get; private set; }

    public int DateKey { get; private set; }

    public int CustomerKey { get; private set; }

    public int ProductKey { get; private set; }

    public int CurrencyKey { get; private set; }

    // Linhagem (docs/09 §5.1, §2: rastreável até o CDM, objeto bruto e execução de conector).
    public Guid SourceSystemId { get; private set; }

    public string SourceEntity { get; private set; }

    public string SourceRecordId { get; private set; }

    public Guid SalesInvoiceId { get; private set; }

    public Guid SalesInvoiceItemId { get; private set; }

    public string RawObjectUri { get; private set; }

    public Guid LoadBatchId { get; private set; }

    public string InvoiceNumber { get; private set; }

    /// <summary>Status da fatura no momento da carga (docs/09 §6.2: "preservar o documento/registro
    /// original quando houver status de cancelamento") — o fato nunca é excluído por cancelamento,
    /// só marcado; a camada semântica (E6) é quem filtra documentos cancelados das métricas.</summary>
    public string Status { get; private set; }

    public int LineNumber { get; private set; }

    public decimal Quantity { get; private set; }

    public decimal GrossAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal? TaxAmount { get; private set; }

    public decimal NetAmount { get; private set; }

    public DateTimeOffset LoadedAt { get; private set; }

    private FactSalesInvoiceItem()
    {
        SourceEntity = string.Empty;
        SourceRecordId = string.Empty;
        RawObjectUri = string.Empty;
        InvoiceNumber = string.Empty;
        Status = string.Empty;
    }

    private FactSalesInvoiceItem(
        Guid tenantId,
        int tenantKey,
        int companyKey,
        int dateKey,
        int customerKey,
        int productKey,
        int currencyKey,
        Guid sourceSystemId,
        string sourceEntity,
        string sourceRecordId,
        Guid salesInvoiceId,
        Guid salesInvoiceItemId,
        string rawObjectUri,
        Guid loadBatchId,
        string invoiceNumber,
        string status,
        int lineNumber,
        decimal quantity,
        decimal grossAmount,
        decimal discountAmount,
        decimal? taxAmount,
        decimal netAmount)
    {
        TenantId = tenantId;
        TenantKey = tenantKey;
        CompanyKey = companyKey;
        DateKey = dateKey;
        CustomerKey = customerKey;
        ProductKey = productKey;
        CurrencyKey = currencyKey;
        SourceSystemId = sourceSystemId;
        SourceEntity = sourceEntity;
        SourceRecordId = sourceRecordId;
        SalesInvoiceId = salesInvoiceId;
        SalesInvoiceItemId = salesInvoiceItemId;
        RawObjectUri = rawObjectUri;
        LoadBatchId = loadBatchId;
        InvoiceNumber = invoiceNumber;
        Status = status;
        LineNumber = lineNumber;
        Quantity = quantity;
        GrossAmount = grossAmount;
        DiscountAmount = discountAmount;
        TaxAmount = taxAmount;
        NetAmount = netAmount;
        LoadedAt = DateTimeOffset.UtcNow;
    }

    public static FactSalesInvoiceItem Create(
        Guid tenantId,
        int tenantKey,
        int companyKey,
        int dateKey,
        int customerKey,
        int productKey,
        int currencyKey,
        Guid sourceSystemId,
        string sourceEntity,
        string sourceRecordId,
        Guid salesInvoiceId,
        Guid salesInvoiceItemId,
        string rawObjectUri,
        Guid loadBatchId,
        string invoiceNumber,
        string status,
        int lineNumber,
        decimal quantity,
        decimal grossAmount,
        decimal discountAmount,
        decimal? taxAmount,
        decimal netAmount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceEntity);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRecordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawObjectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        return new FactSalesInvoiceItem(
            tenantId, tenantKey, companyKey, dateKey, customerKey, productKey, currencyKey,
            sourceSystemId, sourceEntity, sourceRecordId, salesInvoiceId, salesInvoiceItemId,
            rawObjectUri, loadBatchId, invoiceNumber, status, lineNumber, quantity, grossAmount,
            discountAmount, taxAmount, netAmount);
    }

    /// <summary>Recarga idempotente (mesma chave de negócio) — nunca insere uma segunda linha para o
    /// mesmo item de origem.</summary>
    public void ApplyUpdate(
        int tenantKey,
        int companyKey,
        int dateKey,
        int customerKey,
        int productKey,
        int currencyKey,
        Guid salesInvoiceId,
        Guid salesInvoiceItemId,
        string rawObjectUri,
        Guid loadBatchId,
        string invoiceNumber,
        string status,
        decimal quantity,
        decimal grossAmount,
        decimal discountAmount,
        decimal? taxAmount,
        decimal netAmount)
    {
        TenantKey = tenantKey;
        CompanyKey = companyKey;
        DateKey = dateKey;
        CustomerKey = customerKey;
        ProductKey = productKey;
        CurrencyKey = currencyKey;
        SalesInvoiceId = salesInvoiceId;
        SalesInvoiceItemId = salesInvoiceItemId;
        RawObjectUri = rawObjectUri;
        LoadBatchId = loadBatchId;
        InvoiceNumber = invoiceNumber;
        Status = status;
        Quantity = quantity;
        GrossAmount = grossAmount;
        DiscountAmount = discountAmount;
        TaxAmount = taxAmount;
        NetAmount = netAmount;
        LoadedAt = DateTimeOffset.UtcNow;
    }
}
