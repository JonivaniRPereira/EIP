using EIP.Data.Semantic.Application;

namespace EIP.Host.IntegrationTests;

/// <summary>
/// Suporte de teste do E1.4 (`docs/roadmap/fase-2-backlog.md`) — conta quantas vezes o Analytics
/// Engine efetivamente consultou o Warehouse (<see cref="IAnalyticsQueryService.
/// QueryCommercialByDimensionAsync"/>), para provar cache hit/miss sem depender de nenhum detalhe
/// interno do Redis. Singleton na <see cref="HostApiFixture"/> — reiniciar entre testes com
/// <see cref="Reset"/>, já que a coleção inteira compartilha a mesma instância do Host.
/// </summary>
public sealed class AnalyticsQueryCallCounter
{
    private int _count;

    public int Count => _count;

    public void Increment() => Interlocked.Increment(ref _count);

    public void Reset() => Interlocked.Exchange(ref _count, 0);
}

/// <summary>Decora o <see cref="IAnalyticsQueryService"/> real registrado pelo Host só para contar
/// chamadas — nunca altera o comportamento/resultado da consulta em si.</summary>
internal sealed class CountingAnalyticsQueryService : IAnalyticsQueryService
{
    private readonly IAnalyticsQueryService _inner;
    private readonly AnalyticsQueryCallCounter _counter;

    public CountingAnalyticsQueryService(IAnalyticsQueryService inner, AnalyticsQueryCallCounter counter)
    {
        _inner = inner;
        _counter = counter;
    }

    public Task<IReadOnlyList<AnalyticsDimensionRow>> QueryCommercialByDimensionAsync(AnalyticsQueryFilter filter, CancellationToken cancellationToken)
    {
        _counter.Increment();
        return _inner.QueryCommercialByDimensionAsync(filter, cancellationToken);
    }

    public Task<DateTimeOffset?> GetDataFreshnessAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _inner.GetDataFreshnessAsync(tenantId, cancellationToken);
}
