using EIP.Data.Semantic.Application;

namespace EIP.Intelligence.Analytics.Application;

/// <summary>Métrica publicada no catálogo — <see cref="Select"/> extrai o valor certificado
/// correspondente de <see cref="CommercialMetricsResult"/> (E6.1/E1.1, Fase 1/2), nunca uma fórmula
/// própria: o Analytics Engine declarativo reaproveita exatamente as mesmas 3 métricas certificadas,
/// só reorganizando como elas são expostas.</summary>
public sealed record AnalyticsMetricDefinition(string Name, string DisplayName, string Description, Func<CommercialMetricsResult, CertifiedMetricValue> Select);

/// <summary>Dimensão publicada no catálogo — <see cref="EngineDimension"/> é o enum que o agrupamento
/// subjacente (<see cref="IAnalyticsQueryService"/>, E1.1) realmente entende.</summary>
public sealed record AnalyticsDimensionDefinition(string Name, string DisplayName, string Description, AnalyticsDimension EngineDimension);

public sealed record AnalyticsDatasetDefinition(
    string Name,
    string Owner,
    string SemanticVersion,
    IReadOnlyDictionary<string, AnalyticsMetricDefinition> Metrics,
    IReadOnlyDictionary<string, AnalyticsDimensionDefinition> Dimensions);

/// <summary>
/// Catálogo do Analytics Engine (Fase 2, E1.2, docs/10-Analytics-Engine.md §4) — nome técnico,
/// métricas, dimensões, dono e versão de cada dataset consultável, publicado também via
/// <c>GET /api/v1/analytics/datasets</c> (E1.3) para o frontend nunca precisar de hardcode duplicado.
/// Vive em código, mesmo padrão de <c>CertifiedMetrics</c> (Fase 1, E6.1): um catálogo
/// persistido/editável por usuário é explicitamente fora do MVP (docs/10 §14,
/// `docs/roadmap/fase-2-backlog.md` §3). Só o dataset <c>sales</c> nesta fase (fatia Comercial) —
/// <c>product.category</c> deliberadamente ausente das dimensões, mesma lacuna já documentada em
/// <see cref="AnalyticsDimension"/> (E1.1).
/// </summary>
public static class AnalyticsCatalog
{
    public const string SalesDatasetName = "sales";

    public static readonly AnalyticsDatasetDefinition Sales = new(
        Name: SalesDatasetName,
        Owner: "Comercial",
        SemanticVersion: "1.0",
        Metrics: new Dictionary<string, AnalyticsMetricDefinition>(StringComparer.Ordinal)
        {
            ["netRevenue"] = new("netRevenue", "Receita Líquida", CertifiedMetrics.NetRevenue.Description, r => r.NetRevenue),
            ["invoicedQuantity"] = new("invoicedQuantity", "Quantidade Faturada", CertifiedMetrics.InvoicedQuantity.Description, r => r.InvoicedQuantity),
            ["averageTicket"] = new("averageTicket", "Ticket Médio", CertifiedMetrics.AverageTicket.Description, r => r.AverageTicket),
        },
        Dimensions: new Dictionary<string, AnalyticsDimensionDefinition>(StringComparer.Ordinal)
        {
            ["date.month"] = new("date.month", "Mês", "Agrupa por mês da data de emissão da fatura (yyyy-MM).", AnalyticsDimension.DateMonth),
            ["customer"] = new("customer", "Cliente", "Agrupa por cliente (chave de negócio durável).", AnalyticsDimension.Customer),
            ["product"] = new("product", "Produto", "Agrupa por produto (chave de negócio durável).", AnalyticsDimension.Product),
        });

    public static IReadOnlyList<AnalyticsDatasetDefinition> All { get; } = [Sales];

    public static AnalyticsDatasetDefinition? Find(string name) =>
        All.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
}
