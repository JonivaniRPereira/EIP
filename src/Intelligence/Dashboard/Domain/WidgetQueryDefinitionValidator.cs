using EIP.Intelligence.Analytics.Application;

namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>
/// Valida a <see cref="Widget.QueryDefinition"/> de cada widget contra o catálogo do Analytics Engine
/// (E2.3, docs/roadmap/fase-2-backlog.md) — nunca aceita uma consulta inválida num rascunho que será
/// publicado. Só validação estrutural (dataset/métricas/dimensão existem no catálogo); a validação
/// completa de filtros/orderBy/limit já é feita pelo próprio <c>DeclarativeAnalyticsQueryService</c>
/// (E1.2) toda vez que o widget é executado (E3.2) — duplicar essas regras aqui seria redundante sem
/// ganho real. Chamada por <see cref="DashboardVersion.Create"/>, o único ponto de criação de versão.
/// </summary>
internal static class WidgetQueryDefinitionValidator
{
    public static void EnsureValid(Widget widget)
    {
        var dataset = AnalyticsCatalog.Find(widget.QueryDefinition.Dataset);
        if (dataset is null)
        {
            throw new ArgumentException($"Widget '{widget.Title}': dataset '{widget.QueryDefinition.Dataset}' não existe no catálogo do Analytics Engine.", nameof(widget));
        }

        foreach (var metric in widget.QueryDefinition.Metrics)
        {
            if (!dataset.Metrics.ContainsKey(metric))
            {
                throw new ArgumentException($"Widget '{widget.Title}': métrica '{metric}' não existe no dataset '{dataset.Name}'.", nameof(widget));
            }
        }

        foreach (var dimension in widget.QueryDefinition.Dimensions)
        {
            if (!dataset.Dimensions.ContainsKey(dimension))
            {
                throw new ArgumentException($"Widget '{widget.Title}': dimensão '{dimension}' não existe no dataset '{dataset.Name}'.", nameof(widget));
            }
        }
    }
}
