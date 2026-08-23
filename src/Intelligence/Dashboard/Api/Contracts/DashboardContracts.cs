using EIP.Intelligence.Analytics.Application;

namespace EIP.Intelligence.Dashboard.Api.Contracts;

public sealed record CreateDashboardRequest(string Name);

/// <summary>Espelha <see cref="AnalyticsFilterClause"/>, mas com <see cref="Values"/> opcional (vira
/// lista vazia na validação) — mesma tolerância de binding já usada por
/// <c>AnalyticsFilterClauseDto</c> em <c>EIP.Intelligence.Analytics.Api.Contracts</c>.</summary>
public sealed record FilterClauseRequest(string Field, string Operator, IReadOnlyList<string>? Values);

public sealed record OrderByRequest(string Field, string Direction);

/// <summary>Espelha <see cref="AnalyticsQueryDefinition"/> com todo campo (exceto <see cref="Dataset"/>)
/// opcional no JSON — nunca vincula <see cref="AnalyticsQueryDefinition"/> diretamente como corpo de
/// request: suas listas não são anuláveis, então um campo omitido no JSON viraria <see langword="null"/>
/// via desserialização por construtor, quebrando qualquer código a jusante que itera a lista sem checar
/// null.</summary>
public sealed record QueryDefinitionRequest(
    string? Dataset,
    IReadOnlyList<string>? Metrics,
    IReadOnlyList<string>? Dimensions,
    IReadOnlyList<FilterClauseRequest>? Filters,
    IReadOnlyList<OrderByRequest>? OrderBy,
    int? Limit);

public sealed record WidgetVisualizationDto(string? Unit, string? Color);

public sealed record WidgetRequest(string Type, string Title, QueryDefinitionRequest QueryDefinition, WidgetVisualizationDto? Visualization);

public sealed record UpdateDashboardRequest(string? Name, IReadOnlyList<WidgetRequest>? Widgets);

/// <summary>Widget na resposta — <see cref="QueryDefinition"/> aqui é o tipo real do Analytics Engine
/// (E1.2), não a versão tolerante de request: toda saída já vem completa, nunca precisa de
/// preenchimento de campo ausente.</summary>
public sealed record WidgetDto(string Type, string Title, AnalyticsQueryDefinition QueryDefinition, WidgetVisualizationDto Visualization);

public sealed record DashboardDto(Guid Id, string Name, Guid OwnerId, string Status, IReadOnlyList<WidgetDto> Widgets, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record DashboardSummaryDto(Guid Id, string Name, string Status, DateTimeOffset UpdatedAt);

public sealed record PublishDashboardResponse(Guid DashboardId, Guid VersionId, DateTimeOffset PublishedAt);

/// <summary>Metadados de uma execução (docs/10 §5.3) — mesmo shape de
/// <c>EIP.Intelligence.Analytics.Api.Contracts.AnalyticsQueryMetadataDto</c>, redeclarado aqui em vez
/// de referenciado porque um projeto Api nunca referencia outro projeto Api (a Application layer já é
/// o contrato compartilhado entre os dois).</summary>
public sealed record WidgetQueryMetadataDto(string Dataset, string SemanticVersion, DateTimeOffset ExecutedAt, DateTimeOffset? DataFreshnessAt, int RowCount);

public sealed record WidgetQueryResultDto(string WidgetTitle, IReadOnlyList<IReadOnlyDictionary<string, object?>> Data, WidgetQueryMetadataDto Metadata);
