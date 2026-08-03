namespace EIP.Data.Semantic.Application;

/// <summary>Filtro de consulta — <see cref="TenantId"/> vem sempre do claim autenticado, nunca de
/// input de cliente não validado (mesmo princípio de todo o resto do projeto).</summary>
public sealed record MetricsQueryFilter(Guid TenantId, Guid? CompanyId, DateOnly? PeriodStart, DateOnly? PeriodEnd);

/// <summary>Valor de uma métrica certificada, sempre acompanhado da sua definição (docs/09 §9) —
/// nunca um número solto sem dono/versão rastreáveis.</summary>
public sealed record CertifiedMetricValue(CertifiedMetricDefinition Definition, decimal? Value);

public sealed record CommercialMetricsResult(
    CertifiedMetricValue NetRevenue,
    CertifiedMetricValue InvoicedQuantity,
    CertifiedMetricValue AverageTicket);

/// <summary>
/// Camada semântica mínima (E6.1, docs/09 §9) — expõe as 3 métricas certificadas da fatia Comercial
/// a partir de <c>FactSalesInvoiceItem</c>. Não é um motor de métricas genérico/configurável (Fase
/// 2); as fórmulas são fixas no código, cada uma com a definição de <see cref="CertifiedMetrics"/>.
/// </summary>
public interface IMetricsQueryService
{
    Task<CommercialMetricsResult> GetCommercialMetricsAsync(MetricsQueryFilter filter, CancellationToken cancellationToken);
}
