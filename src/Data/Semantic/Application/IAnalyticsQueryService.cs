namespace EIP.Data.Semantic.Application;

/// <summary>Dimensões suportadas pelo agrupamento dimensional do Analytics Engine (Fase 2, E1.1,
/// `docs/roadmap/fase-2-backlog.md` §3). <c>product.category</c> foi deliberadamente omitido: o
/// conector de referência nunca ingere categoria de produto, então <c>DimProduct.CategoryKey</c>
/// permanece sempre nulo nesta fase (mesma lacuna já documentada em <c>DimProductCategory</c>) —
/// agrupar por uma dimensão sempre vazia seria uma funcionalidade de fachada, não real.</summary>
public enum AnalyticsDimension
{
    DateMonth,
    Customer,
    Product,
}

/// <summary>Filtro de consulta dimensional — <see cref="TenantId"/> vem sempre do claim autenticado,
/// nunca de input de cliente não validado (mesmo princípio de <see cref="MetricsQueryFilter"/>).</summary>
public sealed record AnalyticsQueryFilter(Guid TenantId, Guid? CompanyId, DateOnly? PeriodStart, DateOnly? PeriodEnd, AnalyticsDimension Dimension);

/// <summary>Uma linha do agrupamento: <see cref="DimensionKey"/> é o identificador estável do grupo
/// (mês no formato <c>yyyy-MM</c>, ou o Guid de negócio de cliente/produto — nunca a chave substituta
/// versionada de SCD2, ver <c>FactSalesInvoiceItemForMetrics</c>); <see cref="DimensionLabel"/> é o
/// texto amigável para exibição. <see cref="Metrics"/> reaproveita exatamente as mesmas 3 métricas
/// certificadas do E6.1 (Fase 1), agora calculadas por grupo em vez de um único agregado.</summary>
public sealed record AnalyticsDimensionRow(string DimensionKey, string DimensionLabel, CommercialMetricsResult Metrics);

/// <summary>
/// Analytics Engine mínimo (Fase 2, E1.1, `docs/10-Analytics-Engine.md`) sobre a fatia Comercial —
/// agrupa as 3 métricas certificadas por uma dimensão publicada no catálogo, sem nunca aceitar
/// SQL/campo físico arbitrário do cliente. Não é o motor declarativo genérico completo de `docs/10`
/// (sem múltiplas dimensões simultâneas, sem Time Intelligence, sem guardrails de custo) — esses
/// ficam para as tarefas seguintes deste mesmo épico (E1.2 em diante).
/// </summary>
public interface IAnalyticsQueryService
{
    Task<IReadOnlyList<AnalyticsDimensionRow>> QueryCommercialByDimensionAsync(AnalyticsQueryFilter filter, CancellationToken cancellationToken);
}
