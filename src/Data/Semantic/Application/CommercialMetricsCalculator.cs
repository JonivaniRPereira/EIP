using EIP.Data.Warehouse.Application;

namespace EIP.Data.Semantic.Application;

/// <summary>Fórmulas das 3 métricas certificadas (docs/09 §9), compartilhadas entre a consulta
/// agregada única (E6.1, <see cref="IMetricsQueryService"/>) e o agrupamento dimensional do Analytics
/// Engine (Fase 2, E1.1, <see cref="IAnalyticsQueryService"/>) — a mesma fórmula nunca pode divergir
/// entre os dois consumidores.</summary>
internal static class CommercialMetricsCalculator
{
    public const string CanceledStatus = "Canceled";

    public static CommercialMetricsResult Compute(IReadOnlyCollection<FactSalesInvoiceItemForMetrics> validItems)
    {
        var netRevenue = validItems.Sum(i => i.NetAmount);
        var invoicedQuantity = validItems.Sum(i => i.Quantity);
        var distinctInvoiceCount = validItems.Select(i => i.SalesInvoiceId).Distinct().Count();

        // Nenhuma fatura válida no grupo: Ticket Médio não tem significado (evita divisão por zero)
        // — retorna nulo, não zero, para o consumidor não confundir "sem dado" com "zero".
        decimal? averageTicket = distinctInvoiceCount == 0 ? null : netRevenue / distinctInvoiceCount;

        return new CommercialMetricsResult(
            new CertifiedMetricValue(CertifiedMetrics.NetRevenue, netRevenue),
            new CertifiedMetricValue(CertifiedMetrics.InvoicedQuantity, invoicedQuantity),
            new CertifiedMetricValue(CertifiedMetrics.AverageTicket, averageTicket));
    }
}
