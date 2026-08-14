namespace EIP.Intelligence.Analytics.Application;

/// <summary>
/// Analytics Engine declarativo (Fase 2, E1.2, docs/10-Analytics-Engine.md §5.1) — recebe o contrato
/// dataset/métricas/dimensões/filtros/orderBy/limit, valida contra <see cref="AnalyticsCatalog"/>
/// (nunca executa uma consulta inválida) e delega a execução ao contrato já publicado por
/// <c>EIP.Data.Semantic.Application</c> (docs/00: <c>Intelligence</c> nunca acessa
/// <c>Data.Warehouse.*</c> diretamente). <paramref name="tenantId"/> vem sempre do claim JWT
/// autenticado, nunca do corpo da requisição.
/// </summary>
public interface IDeclarativeAnalyticsQueryService
{
    Task<AnalyticsQueryExecutionResult> ExecuteAsync(Guid tenantId, AnalyticsQueryDefinition query, CancellationToken cancellationToken);
}
