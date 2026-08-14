namespace EIP.Intelligence.Analytics.Api.Contracts;

/// <summary>Corpo de <c>POST /api/v1/analytics/query</c> (docs/10-Analytics-Engine.md §5.1). Todos os
/// campos exceto <see cref="Dataset"/> são opcionais no JSON — omitidos viram lista vazia na
/// validação, nunca <see langword="null"/> não tratado.</summary>
public sealed record AnalyticsFilterClauseDto(string Field, string Operator, IReadOnlyList<string>? Values);

public sealed record AnalyticsOrderByDto(string Field, string Direction);

public sealed record AnalyticsQueryRequest(
    string? Dataset,
    IReadOnlyList<string>? Metrics,
    IReadOnlyList<string>? Dimensions,
    IReadOnlyList<AnalyticsFilterClauseDto>? Filters,
    IReadOnlyList<AnalyticsOrderByDto>? OrderBy,
    int? Limit);

/// <summary>Metadados obrigatórios da resposta (docs/10 §5.3).</summary>
public sealed record AnalyticsQueryMetadataDto(string Dataset, string SemanticVersion, DateTimeOffset ExecutedAt, DateTimeOffset? DataFreshnessAt, int RowCount);

/// <summary>Cada linha de <see cref="Data"/> é um objeto plano (dimensão(ões) + métrica(s) pedidas no
/// mesmo nível, exatamente como no exemplo de docs/10 §5.3) — nunca a separação interna
/// dimensão/métrica usada pela camada de aplicação.</summary>
public sealed record AnalyticsQueryResponse(IReadOnlyList<IReadOnlyDictionary<string, object?>> Data, AnalyticsQueryMetadataDto Metadata);
