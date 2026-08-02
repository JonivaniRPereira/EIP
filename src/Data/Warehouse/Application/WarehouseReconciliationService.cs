using EIP.Data.Canonical.Application;

namespace EIP.Data.Warehouse.Application;

/// <summary>Implementação de referência: compara o que está persistido no Modelo Canônico
/// (<c>canonical.SalesInvoiceItems</c>) contra o que foi de fato materializado no Warehouse
/// (<c>warehouse.FactSalesInvoiceItem</c>) para o mesmo conector — mesmo raciocínio de
/// <c>CanonicalReconciliationService</c> (E4.3), um nível adiante na cadeia
/// Fonte → Raw → Canonical → Fact.</summary>
public sealed class WarehouseReconciliationService : IWarehouseReconciliationService
{
    private readonly ICanonicalRecordStore _canonicalRecordStore;
    private readonly IWarehouseLoadStore _warehouseLoadStore;

    public WarehouseReconciliationService(ICanonicalRecordStore canonicalRecordStore, IWarehouseLoadStore warehouseLoadStore)
    {
        _canonicalRecordStore = canonicalRecordStore;
        _warehouseLoadStore = warehouseLoadStore;
    }

    public async Task<CanonicalToFactReconciliationResult> ReconcileSalesInvoiceItemsAsync(
        Guid tenantId,
        Guid sourceSystemId,
        decimal toleranceFraction,
        CancellationToken cancellationToken)
    {
        var (canonicalCount, canonicalNetAmountTotal) = await _canonicalRecordStore.GetSalesInvoiceItemTotalsAsync(tenantId, sourceSystemId, cancellationToken);
        var (factCount, factNetAmountTotal) = await _warehouseLoadStore.GetFactSalesInvoiceItemTotalsAsync(tenantId, sourceSystemId, cancellationToken);

        var countMatches = canonicalCount == factCount;
        var amountWithinTolerance = IsWithinTolerance(canonicalNetAmountTotal, factNetAmountTotal, toleranceFraction);
        var isWithinTolerance = countMatches && amountWithinTolerance;

        var discrepancy = isWithinTolerance
            ? null
            : $"Contagem canônica={canonicalCount}, fato={factCount}; NetAmount canônico={canonicalNetAmountTotal}, fato={factNetAmountTotal} (tolerância={toleranceFraction:P}).";

        return new CanonicalToFactReconciliationResult(canonicalCount, canonicalNetAmountTotal, factCount, factNetAmountTotal, isWithinTolerance, discrepancy);
    }

    private static bool IsWithinTolerance(decimal canonical, decimal fact, decimal toleranceFraction)
    {
        if (canonical == 0m)
        {
            return fact == 0m;
        }

        var difference = Math.Abs(canonical - fact);
        return difference / Math.Abs(canonical) <= toleranceFraction;
    }
}
