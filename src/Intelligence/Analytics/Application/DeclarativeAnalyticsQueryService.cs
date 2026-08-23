using System.Diagnostics;
using EIP.Data.Semantic.Application;
using Microsoft.Extensions.Logging;

namespace EIP.Intelligence.Analytics.Application;

public sealed partial class DeclarativeAnalyticsQueryService : IDeclarativeAnalyticsQueryService
{
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly IAnalyticsQueryCache _cache;
    private readonly TimeSpan _cacheTtl;
    private readonly AnalyticsGuardrailOptions _guardrails;
    private readonly ILogger<DeclarativeAnalyticsQueryService> _logger;

    public DeclarativeAnalyticsQueryService(IAnalyticsQueryService analyticsQueryService, IAnalyticsQueryCache cache, TimeSpan cacheTtl, AnalyticsGuardrailOptions guardrails, ILogger<DeclarativeAnalyticsQueryService> logger)
    {
        _analyticsQueryService = analyticsQueryService;
        _cache = cache;
        _cacheTtl = cacheTtl;
        _guardrails = guardrails;
        _logger = logger;
    }

    public async Task<AnalyticsQueryExecutionResult> ExecuteAsync(Guid tenantId, AnalyticsQueryDefinition query, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

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

                    // Guardrail de período máximo (E1.5, docs/roadmap/fase-2-backlog.md §3) — sem
                    // filtro 'date', a consulta continua sem limite de período nesta fase (mesma
                    // redução de escopo do guardrail de linhas, abaixo: MVP, não o framework completo).
                    if (end.DayNumber - start.DayNumber > _guardrails.MaxPeriodDays)
                    {
                        return AnalyticsQueryExecutionResult.Fail($"Filtro 'date' não pode cobrir mais que {_guardrails.MaxPeriodDays} dias (guardrail de período máximo).");
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

        if (query.Limit is { } requestedLimit && requestedLimit > _guardrails.MaxRows)
        {
            return AnalyticsQueryExecutionResult.Fail($"limit não pode exceder {_guardrails.MaxRows} (guardrail de linhas de resposta).");
        }

        // Frescor entra na chave de cache como "versão de dado" (E1.4, docs/roadmap/fase-2-backlog.md
        // §3): quando o Warehouse recebe uma carga nova, a chave muda sozinha e a consulta antiga vira
        // um cache miss natural, sem precisar de invalidação orientada a evento.
        var freshness = await _analyticsQueryService.GetDataFreshnessAsync(tenantId, cancellationToken);
        var cacheKey = AnalyticsCacheKeyBuilder.Build(tenantId, dataset, query, freshness);

        var rows = await _cache.GetAsync(cacheKey, cancellationToken);
        var cacheHit = rows is not null;
        if (rows is null)
        {
            var filterForEngine = new AnalyticsQueryFilter(tenantId, companyId, periodStart, periodEnd, dimension.EngineDimension);

            // Guardrail de timeout (E1.5) — CancelAfter nunca cancela a requisição HTTP em si (o
            // `cancellationToken` original continua isolado no token vinculado); o `when` abaixo
            // distingue "nosso timeout estourou" de "o cliente cancelou a requisição", que deve
            // continuar propagando normalmente.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_guardrails.QueryTimeout);

            IReadOnlyList<AnalyticsDimensionRow> groups;
            try
            {
                groups = await _analyticsQueryService.QueryCommercialByDimensionAsync(filterForEngine, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AnalyticsQueryExecutionResult.Fail($"Consulta excedeu o tempo limite de {_guardrails.QueryTimeout.TotalSeconds:0}s (guardrail de timeout de execução).");
            }

            var computedRows = groups
                .Select(g => new AnalyticsQueryRow(
                    new Dictionary<string, string>(StringComparer.Ordinal) { [dimensionName] = g.DimensionLabel },
                    metrics.ToDictionary(m => m.Name, m => m.Select(g.Metrics).Value, StringComparer.Ordinal)))
                .ToList();

            // Guardrail de linhas de resposta (E1.5) para consulta sem 'limit' explícito: nunca trunca
            // silenciosamente o resultado ao orçamento — devolve erro claro pedindo um filtro/limit.
            // Com 'limit' explícito, o guardrail já foi checado acima (o cliente pediu exatamente o
            // que vai receber, truncar para o próprio pedido do cliente não é "silencioso").
            if (query.Limit is null && computedRows.Count > _guardrails.MaxRows)
            {
                return AnalyticsQueryExecutionResult.Fail($"A consulta retornaria mais de {_guardrails.MaxRows} linhas; adicione um filtro ou 'limit' explícito (guardrail de linhas de resposta).");
            }

            if (orderBy is not null)
            {
                var descending = string.Equals(orderBy.Direction, "desc", StringComparison.OrdinalIgnoreCase);

                computedRows = string.Equals(orderBy.Field, dimensionName, StringComparison.Ordinal)
                    ? (descending
                        ? computedRows.OrderByDescending(r => r.DimensionValues[dimensionName], StringComparer.Ordinal).ToList()
                        : computedRows.OrderBy(r => r.DimensionValues[dimensionName], StringComparer.Ordinal).ToList())
                    : (descending
                        ? computedRows.OrderByDescending(r => r.MetricValues[orderBy.Field]).ToList()
                        : computedRows.OrderBy(r => r.MetricValues[orderBy.Field]).ToList());
            }

            if (query.Limit is { } limit)
            {
                computedRows = computedRows.Take(limit).ToList();
            }

            rows = computedRows;
            await _cache.SetAsync(cacheKey, rows, _cacheTtl, cancellationToken);
        }

        var metadata = new AnalyticsQueryMetadata(dataset.Name, dataset.SemanticVersion, DateTimeOffset.UtcNow, freshness, rows.Count);

        // Observabilidade (E1.6, docs/10-Analytics-Engine.md §11) — só forma/metadados da consulta
        // (dataset, nomes de métrica/dimensão, tenant, cache hit-ou-miss, duração, contagem de linhas),
        // nunca os valores de dado retornados. CorrelationId entra sozinho via Serilog.Context.LogContext
        // (já empurrado pelo middleware de correlação do Host em toda a requisição, docs/03 §10) — não
        // precisa ser passado explicitamente aqui.
        LogQueryExecuted(dataset.Name, string.Join(',', query.Metrics), dimensionName, tenantId, cacheHit, stopwatch.Elapsed.TotalMilliseconds, rows.Count);

        return AnalyticsQueryExecutionResult.Ok(new AnalyticsQueryResult(rows, metadata));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Analytics query executada: dataset={Dataset} metrics={Metrics} dimension={Dimension} tenantId={TenantId} cacheHit={CacheHit} durationMs={DurationMs} rowCount={RowCount}")]
    private partial void LogQueryExecuted(string dataset, string metrics, string dimension, Guid tenantId, bool cacheHit, double durationMs, int rowCount);
}
