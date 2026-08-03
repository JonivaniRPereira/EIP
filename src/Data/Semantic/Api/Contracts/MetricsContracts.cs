namespace EIP.Data.Semantic.Api.Contracts;

/// <summary>Sempre inclui definição/dono/versão junto do valor (docs/09-Data-Warehouse.md §9) —
/// nunca um número solto sem proveniência rastreável.</summary>
public sealed record CertifiedMetricValueDto(string Name, string Description, string Owner, string Version, decimal? Value);

public sealed record CommercialMetricsResponse(
    CertifiedMetricValueDto NetRevenue,
    CertifiedMetricValueDto InvoicedQuantity,
    CertifiedMetricValueDto AverageTicket);
