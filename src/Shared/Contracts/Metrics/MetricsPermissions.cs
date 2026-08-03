namespace EIP.Shared.Contracts.Metrics;

/// <summary>Códigos de permissão da camada semântica (E6), mesma convenção de
/// <c>EIP.Shared.Contracts.Connectors.ConnectorPermissions</c> — usados nas claims do JWT e em
/// <c>[RequirePermission(...)]</c>. Só leitura: métricas certificadas não têm um fluxo de gestão
/// próprio nesta fase (a definição vive em código, versionada — E6.1).</summary>
public static class MetricsPermissions
{
    public const string MetricsView = "metrics.view";
}
