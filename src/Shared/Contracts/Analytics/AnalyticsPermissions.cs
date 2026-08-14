namespace EIP.Shared.Contracts.Analytics;

/// <summary>Códigos de permissão do Analytics Engine (Fase 2, E1.2), mesma convenção de
/// <c>EIP.Shared.Contracts.Metrics.MetricsPermissions</c> — usados nas claims do JWT e em
/// <c>[RequirePermission(...)]</c>. Só leitura agregada: consultar o catálogo/executar uma consulta
/// declarativa não gerencia nada, mesma política de <c>metrics.view</c> (todos os papéis).</summary>
public static class AnalyticsPermissions
{
    public const string AnalyticsQuery = "analytics.query";
}
