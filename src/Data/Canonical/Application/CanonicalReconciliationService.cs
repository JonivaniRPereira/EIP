namespace EIP.Data.Canonical.Application;

/// <summary>
/// Implementação de referência: compara o relatório de uma sincronização contra o estado persistido
/// (docs/04 §8.3). O lote identificado pelo raw restringe os totais persistidos à mesma
/// extração reportada, incluindo atualizações de registros antigos. O histórico completo
/// continua disponível sem esse filtro para relatórios que tenham esse mesmo escopo.
/// </summary>
public sealed class CanonicalReconciliationService : ICanonicalReconciliationService
{
    private readonly ICanonicalRecordStore _store;

    public CanonicalReconciliationService(ICanonicalRecordStore store)
    {
        _store = store;
    }

    public async Task<SalesInvoiceReconciliationResult> ReconcileSalesInvoicesAsync(
        Guid tenantId,
        Guid sourceSystemId,
        int reportedCount,
        decimal reportedNetAmountTotal,
        decimal toleranceFraction,
        CancellationToken cancellationToken,
        string? rawObjectUri = null)
    {
        var (actualCount, actualNetAmountTotal) = await _store.GetSalesInvoiceTotalsAsync(tenantId, sourceSystemId, cancellationToken, rawObjectUri);

        var countMatches = actualCount == reportedCount;
        var amountWithinTolerance = IsWithinTolerance(reportedNetAmountTotal, actualNetAmountTotal, toleranceFraction);
        var isWithinTolerance = countMatches && amountWithinTolerance;

        var discrepancy = isWithinTolerance
            ? null
            : $"Contagem reportada={reportedCount}, persistida={actualCount}; NetAmount reportado={reportedNetAmountTotal}, persistido={actualNetAmountTotal} (tolerância={toleranceFraction:P}).";

        return new SalesInvoiceReconciliationResult(reportedCount, reportedNetAmountTotal, actualCount, actualNetAmountTotal, isWithinTolerance, discrepancy);
    }

    private static bool IsWithinTolerance(decimal reported, decimal actual, decimal toleranceFraction)
    {
        if (reported == 0m)
        {
            return actual == 0m;
        }

        var difference = Math.Abs(reported - actual);
        return difference / Math.Abs(reported) <= toleranceFraction;
    }
}
