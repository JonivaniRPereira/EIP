using EIP.Data.Semantic.Application;

namespace EIP.Intelligence.Analytics.Application;

public sealed class DeclarativeAnalyticsQueryService : IDeclarativeAnalyticsQueryService
{
    private readonly IAnalyticsQueryService _analyticsQueryService;

    public DeclarativeAnalyticsQueryService(IAnalyticsQueryService analyticsQueryService)
    {
        _analyticsQueryService = analyticsQueryService;
    }

    public async Task<AnalyticsQueryExecutionResult> ExecuteAsync(Guid tenantId, AnalyticsQueryDefinition query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Dataset))
        {
            return AnalyticsQueryExecutionResult.Fail("dataset é obrigatório.");
        }

        var dataset = AnalyticsCatalog.Find(query.Dataset);
        if (dataset is null)
        {
            return AnalyticsQueryExecutionResult.Fail($"Dataset '{query.Dataset}' não existe no catálogo.");
        }

        if (query.Metrics is not { Count: > 0 })
        {
            return AnalyticsQueryExecutionResult.Fail("metrics deve conter ao menos 1 métrica.");
        }

        var metrics = new List<AnalyticsMetricDefinition>();
        foreach (var metricName in query.Metrics)
        {
            if (!dataset.Metrics.TryGetValue(metricName, out var metric))
            {
                return AnalyticsQueryExecutionResult.Fail($"Métrica '{metricName}' não existe no dataset '{dataset.Name}'.");
            }

            metrics.Add(metric);
        }

        // Esta fase do Analytics Engine exige exatamente 1 dimensão: o agrupamento subjacente
        // (IAnalyticsQueryService, E1.1) só suporta uma por consulta — múltiplas dimensões
        // simultâneas são o motor genérico completo de docs/10, fora do escopo desta fase
        // (docs/roadmap/fase-2-backlog.md §3).
        if (query.Dimensions is not { Count: 1 })
        {
            return AnalyticsQueryExecutionResult.Fail("dimensions deve conter exatamente 1 dimensão nesta fase do Analytics Engine.");
        }

        var dimensionName = query.Dimensions[0];
        if (!dataset.Dimensions.TryGetValue(dimensionName, out var dimension))
        {
            return AnalyticsQueryExecutionResult.Fail($"Dimensão '{dimensionName}' não existe no dataset '{dataset.Name}'.");
        }

        Guid? companyId = null;
        DateOnly? periodStart = null;
        DateOnly? periodEnd = null;

        foreach (var filter in query.Filters)
        {
            switch (filter.Field)
            {
                case "date":
                    if (!string.Equals(filter.Operator, "between", StringComparison.Ordinal)
                        || filter.Values.Count != 2
                        || !DateOnly.TryParse(filter.Values[0], out var start)
                        || !DateOnly.TryParse(filter.Values[1], out var end)
                        || start > end)
                    {
                        return AnalyticsQueryExecutionResult.Fail("Filtro 'date' exige operador 'between' com exatamente 2 datas válidas (início <= fim).");
                    }

                    periodStart = start;
                    periodEnd = end;
                    break;

                case "company.id":
                    if (!string.Equals(filter.Operator, "equals", StringComparison.Ordinal)
                        || filter.Values.Count != 1
                        || !Guid.TryParse(filter.Values[0], out var company))
                    {
                        return AnalyticsQueryExecutionResult.Fail("Filtro 'company.id' exige operador 'equals' com exatamente 1 Guid válido.");
                    }

                    companyId = company;
                    break;

                default:
                    return AnalyticsQueryExecutionResult.Fail($"Campo de filtro '{filter.Field}' não é suportado nesta fase.");
            }
        }

        if (query.OrderBy.Count > 1)
        {
            return AnalyticsQueryExecutionResult.Fail("orderBy suporta no máximo 1 critério nesta fase.");
        }

        var orderBy = query.OrderBy.Count == 1 ? query.OrderBy[0] : null;
        if (orderBy is not null)
        {
            var directionValid = string.Equals(orderBy.Direction, "asc", StringComparison.OrdinalIgnoreCase)
                || string.Equals(orderBy.Direction, "desc", StringComparison.OrdinalIgnoreCase);
            var fieldValid = string.Equals(orderBy.Field, dimensionName, StringComparison.Ordinal)
                || query.Metrics.Contains(orderBy.Field, StringComparer.Ordinal);

            if (!directionValid || !fieldValid)
            {
                return AnalyticsQueryExecutionResult.Fail("orderBy.field deve ser a dimensão ou uma das métricas solicitadas, e orderBy.direction deve ser 'asc' ou 'desc'.");
            }
        }

        if (query.Limit is <= 0)
        {
            return AnalyticsQueryExecutionResult.Fail("limit, quando informado, deve ser maior que zero.");
        }

        var filterForEngine = new AnalyticsQueryFilter(tenantId, companyId, periodStart, periodEnd, dimension.EngineDimension);
        var groups = await _analyticsQueryService.QueryCommercialByDimensionAsync(filterForEngine, cancellationToken);

        var rows = groups
            .Select(g => new AnalyticsQueryRow(
                new Dictionary<string, string>(StringComparer.Ordinal) { [dimensionName] = g.DimensionLabel },
                metrics.ToDictionary(m => m.Name, m => m.Select(g.Metrics).Value, StringComparer.Ordinal)))
            .ToList();

        if (orderBy is not null)
        {
            var descending = string.Equals(orderBy.Direction, "desc", StringComparison.OrdinalIgnoreCase);

            rows = string.Equals(orderBy.Field, dimensionName, StringComparison.Ordinal)
                ? (descending
                    ? rows.OrderByDescending(r => r.DimensionValues[dimensionName], StringComparer.Ordinal).ToList()
                    : rows.OrderBy(r => r.DimensionValues[dimensionName], StringComparer.Ordinal).ToList())
                : (descending
                    ? rows.OrderByDescending(r => r.MetricValues[orderBy.Field]).ToList()
                    : rows.OrderBy(r => r.MetricValues[orderBy.Field]).ToList());
        }

        if (query.Limit is { } limit)
        {
            rows = rows.Take(limit).ToList();
        }

        var freshness = await _analyticsQueryService.GetDataFreshnessAsync(tenantId, cancellationToken);
        var metadata = new AnalyticsQueryMetadata(dataset.Name, dataset.SemanticVersion, DateTimeOffset.UtcNow, freshness, rows.Count);

        return AnalyticsQueryExecutionResult.Ok(new AnalyticsQueryResult(rows, metadata));
    }
}
