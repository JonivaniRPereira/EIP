namespace EIP.Intelligence.Analytics.Application;

/// <summary>
/// Cache do Analytics Engine (Fase 2, E1.4, ADR-006 — primeiro uso funcional do Redis desde que subiu
/// no E1 da Fase 0, até aqui só com health check). A chave já incorpora tenant, dataset, versão
/// semântica, consulta normalizada e versão de dado (<see cref="AnalyticsCacheKeyBuilder"/>), então
/// esta interface nunca precisa saber nada sobre isolamento de tenant por conta própria — nunca serve
/// dado de um tenant para outro porque a chave em si já os separa. O backend real
/// (<c>EIP.Intelligence.Analytics.Infrastructure.RedisAnalyticsQueryCache</c>) vive fora desta camada;
/// aqui só o contrato, mesmo padrão de <c>ICanonicalRecordStore</c>/<c>IWarehouseLoadStore</c>.
/// </summary>
public interface IAnalyticsQueryCache
{
    Task<IReadOnlyList<AnalyticsQueryRow>?> GetAsync(string key, CancellationToken cancellationToken);

    Task SetAsync(string key, IReadOnlyList<AnalyticsQueryRow> rows, TimeSpan ttl, CancellationToken cancellationToken);
}
