using EIP.Intelligence.Analytics.Application;

namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>
/// Um widget dentro de uma <see cref="DashboardVersion"/> — Value Object, nunca uma tabela própria
/// (docs/12 §3: "nunca é consultado isoladamente fora do próprio dashboard"). <see cref="QueryDefinition"/>
/// é literalmente o mesmo contrato declarativo do Analytics Engine (E1.2) — validado contra o catálogo
/// (E2.3) e executado por ele (E3.2), nunca por consulta própria do Dashboard Builder.
/// </summary>
public sealed record Widget(WidgetType Type, string Title, AnalyticsQueryDefinition QueryDefinition, WidgetVisualization Visualization);
