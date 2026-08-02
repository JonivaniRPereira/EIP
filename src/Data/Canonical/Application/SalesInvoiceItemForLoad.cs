using EIP.Data.Canonical.Domain;

namespace EIP.Data.Canonical.Application;

/// <summary>
/// Projeção plana de um item de fatura canônico + a fatura e as entidades de referência
/// (cliente/produto) que ele resolve, usada só pela carga do Data Warehouse (E5.3,
/// docs/09-Data-Warehouse.md §7.1: "receber lote válido do Modelo Canônico"). Existe para o
/// Warehouse nunca precisar conhecer o schema/EF Core do Canônico diretamente — só este contrato.
/// </summary>
public sealed record SalesInvoiceItemForLoad(
    Guid TenantId,
    Guid CompanyId,
    Guid SourceSystemId,
    string SourceEntity,
    string SourceRecordId,
    string RawObjectUri,
    Guid SalesInvoiceId,
    Guid SalesInvoiceItemId,
    string InvoiceNumber,
    DateOnly IssueDate,
    string CurrencyCode,
    SalesInvoiceStatus Status,
    int LineNumber,
    decimal Quantity,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal? TaxAmount,
    decimal NetAmount,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    string? CustomerCity,
    string? CustomerStateOrRegion,
    string? CustomerCountryCode,
    bool CustomerIsActive,
    Guid ProductId,
    string ProductCode,
    string ProductName,
    ProductType ProductType,
    string? ProductUnitOfMeasure,
    bool ProductIsActive);
