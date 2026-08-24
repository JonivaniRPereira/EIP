using EIP.Data.Semantic.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Warehouse.Application;
using EIP.Shared.Contracts.Canonical;

namespace EIP.Data.Catalog.Application;

/// <summary>
/// Implementação de <see cref="IMetricsCatalogService"/>: agrega os metadados estáticos de
/// <see cref="CertifiedMetrics"/> com uma reconciliação Canônico↔Fato calculada ao vivo
/// (<see cref="IWarehouseReconciliationService"/>) para cada instância de conector do tenant que
/// alimenta a fatia Comercial — as 3 métricas certificadas desta fase são todas sobre
/// <c>FactSalesInvoiceItem</c>, então compartilham a mesma reconciliação por origem.
/// </summary>
public sealed class MetricsCatalogService : IMetricsCatalogService
{
    // Mesma tolerância usada por ConnectorSyncProcessor ao reconciliar durante a sincronização
    // (docs/04 §8.3) — este endpoint só torna essa mesma verificação consultável sob demanda, nunca
    // usa um critério diferente.
    private const decimal ReconciliationToleranceFraction = 0.01m;

    private static readonly IReadOnlyList<CertifiedMetricDefinition> Definitions =
    [
        CertifiedMetrics.NetRevenue,
        CertifiedMetrics.InvoicedQuantity,
        CertifiedMetrics.AverageTicket,
    ];

    private readonly IConnectorSyncStore _connectorSyncStore;
    private readonly IWarehouseReconciliationService _warehouseReconciliationService;

    public MetricsCatalogService(IConnectorSyncStore connectorSyncStore, IWarehouseReconciliationService warehouseReconciliationService)
    {
        _connectorSyncStore = connectorSyncStore;
        _warehouseReconciliationService = warehouseReconciliationService;
    }

    public async Task<IReadOnlyList<MetricCatalogEntry>> ListMetricsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var instances = await _connectorSyncStore.ListInstancesAsync(tenantId, cancellationToken);

        // As 3 métricas certificadas desta fase são todas da fatia Comercial (SalesInvoiceItem) — só
        // origens de sales-invoices participam da reconciliação; outra origem (ex.: "customers") não
        // alimenta FactSalesInvoiceItem e reconciliar contra ela sempre daria "0 == 0", um falso
        // positivo enganoso.
        var salesInvoiceInstances = instances.Where(i => i.SourceEntity == CanonicalSourceEntities.SalesInvoices).ToList();

        var reconciliations = new List<MetricReconciliationSnapshot>(salesInvoiceInstances.Count);
        foreach (var instance in salesInvoiceInstances)
        {
            var result = await _warehouseReconciliationService.ReconcileSalesInvoiceItemsAsync(
                tenantId, instance.Id, ReconciliationToleranceFraction, cancellationToken);

            reconciliations.Add(new MetricReconciliationSnapshot(
                instance.Id,
                instance.Name,
                result.IsWithinTolerance,
                result.CanonicalCount,
                result.CanonicalNetAmountTotal,
                result.FactCount,
                result.FactNetAmountTotal,
                result.Discrepancy));
        }

        return Definitions
            .Select(definition => new MetricCatalogEntry(definition.Name, definition.Description, definition.Owner, definition.Version, reconciliations))
            .ToList();
    }
}
