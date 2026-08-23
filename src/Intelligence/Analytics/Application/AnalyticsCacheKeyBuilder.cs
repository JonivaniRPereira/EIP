using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EIP.Intelligence.Analytics.Application;

/// <summary>
/// Monta a chave de cache do Analytics Engine (Fase 2, E1.4, `docs/roadmap/fase-2-backlog.md` §3):
/// <c>tenant + dataset + versão semântica + consulta normalizada + versão de dado</c> — reduzida de
/// `docs/10-Analytics-Engine.md` §8.1 (sem <c>workspace</c>, que não existe nesta plataforma).
/// <see cref="AnalyticsCacheKeyBuilder"/> não conhece Redis nem nenhum backend de cache: só computa a
/// string determinística que <see cref="IAnalyticsQueryCache"/> usa como chave.
/// </summary>
public static class AnalyticsCacheKeyBuilder
{
    /// <summary>
    /// <paramref name="dataVersion"/> é o frescor do dado do tenant (<c>IAnalyticsQueryService.
    /// GetDataFreshnessAsync</c>) — quando o Warehouse recebe uma nova carga, a chave muda sozinha e a
    /// consulta antiga vira um cache miss natural. Não é invalidação ativa (a entrada antiga só expira
    /// pelo TTL, nunca é apagada), mas cumpre o mesmo objetivo prático sem exigir um mecanismo de
    /// notificação orientado a evento — que `docs/10 §8.1` descreve como ideal, mas que ainda não
    /// existe na plataforma (escopo desta fase, mesma redução documentada no backlog).
    /// </summary>
    public static string Build(Guid tenantId, AnalyticsDatasetDefinition dataset, AnalyticsQueryDefinition query, DateTimeOffset? dataVersion)
    {
        var normalized = new NormalizedQuery(
            query.Dataset,
            [.. query.Metrics.OrderBy(m => m, StringComparer.Ordinal)],
            [.. query.Dimensions.OrderBy(d => d, StringComparer.Ordinal)],
            [.. query.Filters
                .Select(f => new NormalizedFilter(f.Field, f.Operator, f.Values))
                .OrderBy(f => f.Field, StringComparer.Ordinal)],
            [.. query.OrderBy.Select(o => new NormalizedOrderBy(o.Field, o.Direction))],
            query.Limit);

        var normalizedJson = JsonSerializer.Serialize(normalized);
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedJson)));
        var dataVersionToken = dataVersion?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture) ?? "none";

        return $"analytics:{tenantId:N}:{dataset.Name}:{dataset.SemanticVersion}:{dataVersionToken}:{hash}";
    }

    private sealed record NormalizedQuery(string Dataset, IReadOnlyList<string> Metrics, IReadOnlyList<string> Dimensions, IReadOnlyList<NormalizedFilter> Filters, IReadOnlyList<NormalizedOrderBy> OrderBy, int? Limit);

    private sealed record NormalizedFilter(string Field, string Operator, IReadOnlyList<string> Values);

    private sealed record NormalizedOrderBy(string Field, string Direction);
}
