using EIP.Data.Warehouse.Application;

namespace EIP.Data.Semantic.Application;

public sealed class AnalyticsQueryService : IAnalyticsQueryService
{
    private readonly IWarehouseLoadStore _warehouseLoadStore;

    public AnalyticsQueryService(IWarehouseLoadStore warehouseLoadStore)
    {
        _warehouseLoadStore = warehouseLoadStore;
    }

    public async Task<IReadOnlyList<AnalyticsDimensionRow>> QueryCommercialByDimensionAsync(AnalyticsQueryFilter filter, CancellationToken cancellationToken)
    {
        var items = await _warehouseLoadStore.ListFactSalesInvoiceItemsForMetricsAsync(
            filter.TenantId, filter.CompanyId, filter.PeriodStart, filter.PeriodEnd, cancellationToken);

        var validItems = items.Where(i => i.Status != CommercialMetricsCalculator.CanceledStatus);

        var groups = filter.Dimension switch
        {
            AnalyticsDimension.DateMonth => validItems.GroupBy(i => FormatMonth(i.DateKey), i => i)
                .Select(g => (Key: g.Key, Label: g.Key, Items: (IReadOnlyCollection<FactSalesInvoiceItemForMetrics>)g.ToList())),
            AnalyticsDimension.Customer => validItems.GroupBy(i => i.CustomerId)
                .Select(g => (Key: g.Key.ToString(), Label: g.First().CustomerName, Items: (IReadOnlyCollection<FactSalesInvoiceItemForMetrics>)g.ToList())),
            AnalyticsDimension.Product => validItems.GroupBy(i => i.ProductId)
                .Select(g => (Key: g.Key.ToString(), Label: g.First().ProductName, Items: (IReadOnlyCollection<FactSalesInvoiceItemForMetrics>)g.ToList())),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter.Dimension, "Dimensão não suportada pelo Analytics Engine."),
        };

        return groups
            .Select(g => new AnalyticsDimensionRow(g.Key, g.Label, CommercialMetricsCalculator.Compute(g.Items)))
            .OrderBy(r => r.DimensionKey, StringComparer.Ordinal)
            .ToList();
    }

    // DateKey é sempre YYYYMMDD (docs/09 §5.1) — extrai ano/mês por aritmética, sem precisar de
    // junção com DimDate, mesmo princípio já usado no filtro de período de
    // ListFactSalesInvoiceItemsForMetricsAsync.
    private static string FormatMonth(int dateKey)
    {
        var year = dateKey / 10000;
        var month = dateKey / 100 % 100;
        return $"{year:D4}-{month:D2}";
    }
}
