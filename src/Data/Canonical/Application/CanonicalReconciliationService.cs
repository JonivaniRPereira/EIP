namespace EIP.Data.Canonical.Application;

/// <summary>
/// Implementação de referência: compara o relatório de uma sincronização contra o estado persistido
/// (docs/04 §8.3). Neste conector de referência (extração completa a cada sincronização, sem
/// watermark/incremental ainda — E7.1/E7.2 da Fase 1), "reportado nesta execução" e "persistido no
/// total" devem coincidir sempre que nada deu errado; uma divergência aqui é sinal de um bug real
/// (ex. commit parcial), não de uma sincronização incremental legítima.
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
        CancellationToken cancellationToken)
    {
        var (actualCount, actualNetAmountTotal) = await _store.GetSalesInvoiceTotalsAsync(tenantId, sourceSystemId, cancellationToken);

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
