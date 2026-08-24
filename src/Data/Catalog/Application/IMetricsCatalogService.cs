namespace EIP.Data.Catalog.Application;

/// <summary>Reconciliação Canônico↔Fato (docs/09-Data-Warehouse.md §8.2) para uma origem específica —
/// sempre por <see cref="ConnectorInstanceId"/>, nunca agregada às cegas entre origens diferentes de
/// um mesmo tenant. Computada ao vivo a cada consulta (não existe uma tabela de histórico de
/// reconciliações nesta fase) — é sempre "a reconciliação mais recente possível", nunca uma
/// fotografia potencialmente desatualizada.</summary>
public sealed record MetricReconciliationSnapshot(
    Guid ConnectorInstanceId,
    string ConnectorInstanceName,
    bool IsWithinTolerance,
    int CanonicalCount,
    decimal CanonicalNetAmountTotal,
    int FactCount,
    decimal FactNetAmountTotal,
    string? Discrepancy);

/// <summary>Metadados completos de uma métrica certificada (Fase 3, E3.2) — mais do que o
/// `GET /api/v1/analytics/datasets` da Fase 2 expõe (só nome/descrição): fórmula em texto, versão,
/// dono e a reconciliação conhecida por origem. Nenhum campo aqui duplica o que já vive em
/// `CertifiedMetrics`/`IWarehouseReconciliationService` — apenas agrega o que já existe.</summary>
public sealed record MetricCatalogEntry(
    string Name,
    string FormulaText,
    string Owner,
    string Version,
    IReadOnlyList<MetricReconciliationSnapshot> Reconciliations);

public interface IMetricsCatalogService
{
    Task<IReadOnlyList<MetricCatalogEntry>> ListMetricsAsync(Guid tenantId, CancellationToken cancellationToken);
}
