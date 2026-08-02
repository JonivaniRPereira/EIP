namespace EIP.Data.Warehouse.Application;

/// <summary>Resultado de uma reconciliação Canônico↔Fato (docs/09-Data-Warehouse.md §8.2) para
/// <c>FactSalesInvoiceItem</c>.</summary>
public sealed record CanonicalToFactReconciliationResult(
    int CanonicalCount,
    decimal CanonicalNetAmountTotal,
    int FactCount,
    decimal FactNetAmountTotal,
    bool IsWithinTolerance,
    string? Discrepancy);

/// <summary>
/// Verificação de totais entre o Modelo Canônico e o fato materializado (docs/09 §8.2: "a EIP deve
/// permitir comparar Fonte → Raw → Canonical → Fact/Data Mart"; docs/roadmap/fase-1-backlog.md E5.4).
/// O bloqueio automático de publicação por divergência fica para fase futura — aqui a verificação só
/// precisa existir e ser testável (mesmo critério já aplicado à reconciliação Canônico↔Origem, E4.3).
/// </summary>
public interface IWarehouseReconciliationService
{
    Task<CanonicalToFactReconciliationResult> ReconcileSalesInvoiceItemsAsync(
        Guid tenantId,
        Guid sourceSystemId,
        decimal toleranceFraction,
        CancellationToken cancellationToken);
}
