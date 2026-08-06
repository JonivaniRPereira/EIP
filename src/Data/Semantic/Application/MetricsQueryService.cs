using EIP.Data.Warehouse.Application;

namespace EIP.Data.Semantic.Application;

/// <summary>Implementação de referência das 3 métricas certificadas (docs/09 §9). "Faturas/itens
/// válidos" (linguagem do docs/09) significa: status diferente de <c>Canceled</c> — o mesmo status
/// já preservado (nunca excluído) em <c>FactSalesInvoiceItem.Status</c> desde a carga (docs/09
/// §6.2).</summary>
public sealed class MetricsQueryService : IMetricsQueryService
{
    private readonly IWarehouseLoadStore _warehouseLoadStore;

    public MetricsQueryService(IWarehouseLoadStore warehouseLoadStore)
    {
        _warehouseLoadStore = warehouseLoadStore;
    }

    public async Task<CommercialMetricsResult> GetCommercialMetricsAsync(MetricsQueryFilter filter, CancellationToken cancellationToken)
    {
        var items = await _warehouseLoadStore.ListFactSalesInvoiceItemsForMetricsAsync(
            filter.TenantId, filter.CompanyId, filter.PeriodStart, filter.PeriodEnd, cancellationToken);

        var validItems = items.Where(i => i.Status != CommercialMetricsCalculator.CanceledStatus).ToList();

        return CommercialMetricsCalculator.Compute(validItems);
    }
}
