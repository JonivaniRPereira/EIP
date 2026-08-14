namespace EIP.Intelligence.Analytics.Application;

/// <summary>Cláusula de filtro do contrato declarativo (docs/10-Analytics-Engine.md §5.1/§5.2).
/// Nesta fase só <c>date</c> (operador <c>between</c>, 2 datas) e <c>company.id</c> (operador
/// <c>equals</c>, 1 Guid) são suportados — nunca SQL, nome físico de coluna ou expressão livre.</summary>
public sealed record AnalyticsFilterClause(string Field, string Operator, IReadOnlyList<string> Values);

/// <summary><see cref="Direction"/> é <c>asc</c> ou <c>desc</c>. <see cref="Field"/> deve ser a
/// dimensão solicitada ou uma das métricas solicitadas — nunca um campo fora da própria consulta.</summary>
public sealed record AnalyticsOrderBy(string Field, string Direction);

/// <summary>
/// Contrato declarativo de consulta (docs/10 §5.1) — nunca SQL do cliente. Reduzido do motor genérico
/// completo (`docs/roadmap/fase-2-backlog.md` §3): exatamente 1 dimensão por consulta (o agrupamento
/// subjacente do E1.1 só suporta uma), no máximo 1 critério de ordenação.
/// </summary>
public sealed record AnalyticsQueryDefinition(
    string Dataset,
    IReadOnlyList<string> Metrics,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<AnalyticsFilterClause> Filters,
    IReadOnlyList<AnalyticsOrderBy> OrderBy,
    int? Limit);

/// <summary>Uma linha de resultado — <see cref="DimensionValues"/> tem exatamente 1 entrada nesta
/// fase (chave = nome técnico da dimensão, valor = <c>AnalyticsDimensionRow.DimensionLabel</c>, mesmo
/// texto amigável já usado pela camada semântica); <see cref="MetricValues"/> só contém as métricas
/// pedidas na consulta, nunca as 3 certificadas inteiras se o cliente pediu menos.</summary>
public sealed record AnalyticsQueryRow(IReadOnlyDictionary<string, string> DimensionValues, IReadOnlyDictionary<string, decimal?> MetricValues);

/// <summary>Metadados obrigatórios da resposta (docs/10 §5.3): dataset, versão semântica, momento de
/// execução, frescor do dado (<see langword="null"/> se o tenant nunca teve uma carga concluída) e
/// contagem de linhas.</summary>
public sealed record AnalyticsQueryMetadata(
    string Dataset,
    string SemanticVersion,
    DateTimeOffset ExecutedAt,
    DateTimeOffset? DataFreshnessAt,
    int RowCount);

public sealed record AnalyticsQueryResult(IReadOnlyList<AnalyticsQueryRow> Data, AnalyticsQueryMetadata Metadata);

/// <summary>Resultado da execução — nunca lança exceção para um erro de validação de consulta
/// (campo/métrica/dimensão inexistente), sempre retorna <see cref="Error"/> com uma mensagem clara,
/// mesmo padrão de <c>ConnectorSyncRequestResult</c> (Fase 0, E7).</summary>
public sealed record AnalyticsQueryExecutionResult
{
    public bool Success { get; }

    public string? Error { get; }

    public AnalyticsQueryResult? Result { get; }

    private AnalyticsQueryExecutionResult(bool success, string? error, AnalyticsQueryResult? result)
    {
        Success = success;
        Error = error;
        Result = result;
    }

    public static AnalyticsQueryExecutionResult Ok(AnalyticsQueryResult result) => new(true, null, result);

    public static AnalyticsQueryExecutionResult Fail(string error) => new(false, error, null);
}
