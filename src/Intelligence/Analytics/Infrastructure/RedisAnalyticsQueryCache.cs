using System.Text.Json;
using EIP.Intelligence.Analytics.Application;
using Microsoft.Extensions.Caching.Distributed;

namespace EIP.Intelligence.Analytics.Infrastructure;

/// <summary>
/// Backend real do <see cref="IAnalyticsQueryCache"/> (Fase 2, E1.4, ADR-006) — só depende de
/// <see cref="IDistributedCache"/>, então não sabe (nem precisa saber) que o backend por trás é Redis;
/// isso é decidido inteiramente pela composição de DI do Host (<c>AddStackExchangeRedisCache</c>).
/// Serializa as linhas de resultado em um DTO próprio (<see cref="CachedRow"/>) porque
/// <see cref="AnalyticsQueryRow"/> expõe <c>IReadOnlyDictionary&lt;,&gt;</c>, que o
/// <c>System.Text.Json</c> não desserializa diretamente sem um conversor customizado.
/// </summary>
public sealed class RedisAnalyticsQueryCache : IAnalyticsQueryCache
{
    private readonly IDistributedCache _cache;

    public RedisAnalyticsQueryCache(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<IReadOnlyList<AnalyticsQueryRow>?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var bytes = await _cache.GetAsync(key, cancellationToken);
        if (bytes is null)
        {
            return null;
        }

        var cachedRows = JsonSerializer.Deserialize<List<CachedRow>>(bytes)
            ?? throw new InvalidOperationException($"Entrada de cache '{key}' corrompida.");

        return cachedRows
            .Select(r => new AnalyticsQueryRow(r.DimensionValues, r.MetricValues))
            .ToList();
    }

    public async Task SetAsync(string key, IReadOnlyList<AnalyticsQueryRow> rows, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var cachedRows = rows
            .Select(r => new CachedRow(
                new Dictionary<string, string>(r.DimensionValues, StringComparer.Ordinal),
                new Dictionary<string, decimal?>(r.MetricValues, StringComparer.Ordinal)))
            .ToList();

        var bytes = JsonSerializer.SerializeToUtf8Bytes(cachedRows);
        await _cache.SetAsync(key, bytes, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, cancellationToken);
    }

    private sealed record CachedRow(Dictionary<string, string> DimensionValues, Dictionary<string, decimal?> MetricValues);
}
