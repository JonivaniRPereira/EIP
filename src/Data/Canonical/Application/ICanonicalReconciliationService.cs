namespace EIP.Data.Canonical.Application;

/// <summary>Resultado de uma reconciliação Canônico↔Origem (docs/04-Modelo-Canonico.md §8.3) para a
/// fatia Comercial. <see cref="ReportedCount"/>/<see cref="ReportedNetAmountTotal"/> vêm do relatório
/// do <c>SyncRun</c> (E4.1); <see cref="ActualCount"/>/<see cref="ActualNetAmountTotal"/> vêm do que
/// está de fato persistido no Modelo Canônico agora.</summary>
public sealed record SalesInvoiceReconciliationResult(
    int ReportedCount,
    decimal ReportedNetAmountTotal,
    int ActualCount,
    decimal ActualNetAmountTotal,
    bool IsWithinTolerance,
    string? Discrepancy);

/// <summary>
/// Verificação de totais entre o que uma sincronização reportou e o que está persistido (docs/04
/// §8.3: "devem existir verificações de totais por período/origem que permitam comparar EIP e
/// sistema fonte"). O bloqueio automático de publicação por divergência fica para E5/E6
/// (docs/roadmap/fase-1-backlog.md E4.3) — aqui a verificação só precisa existir e ser testável.
/// </summary>
public interface ICanonicalReconciliationService
{
    Task<SalesInvoiceReconciliationResult> ReconcileSalesInvoicesAsync(
        Guid tenantId,
        Guid sourceSystemId,
        int reportedCount,
        decimal reportedNetAmountTotal,
        decimal toleranceFraction,
        CancellationToken cancellationToken);
}
