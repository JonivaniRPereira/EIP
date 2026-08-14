using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.Analytics.Api.Contracts;
using EIP.Intelligence.Analytics.Application;
using EIP.Shared.Contracts.Analytics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Intelligence.Analytics.Api;

/// <summary>
/// Analytics Engine declarativo (Fase 2, E1.2, docs/10-Analytics-Engine.md §5) — único ponto de
/// consulta sobre a fatia Comercial exposto ao frontend/dashboards/IA. Nunca aceita SQL ou nome
/// físico de tabela do Warehouse; tudo é validado contra <see cref="AnalyticsCatalog"/> antes de
/// qualquer execução (docs/10 §6.1).
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IDeclarativeAnalyticsQueryService _queryService;

    public AnalyticsController(IDeclarativeAnalyticsQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpPost("query")]
    [RequirePermission(AnalyticsPermissions.AnalyticsQuery)]
    public async Task<ActionResult<AnalyticsQueryResponse>> Query(AnalyticsQueryRequest request, CancellationToken cancellationToken)
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var definition = new AnalyticsQueryDefinition(
            request.Dataset ?? string.Empty,
            request.Metrics ?? [],
            request.Dimensions ?? [],
            (request.Filters ?? [])
                .Select(f => new AnalyticsFilterClause(f.Field, f.Operator, f.Values ?? []))
                .ToList(),
            (request.OrderBy ?? [])
                .Select(o => new AnalyticsOrderBy(o.Field, o.Direction))
                .ToList(),
            request.Limit);

        var result = await _queryService.ExecuteAsync(tenantId, definition, cancellationToken);
        if (!result.Success)
        {
            return Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        var executed = result.Result!;
        var response = new AnalyticsQueryResponse(
            executed.Data.Select(ToFlatRow).ToList(),
            new AnalyticsQueryMetadataDto(
                executed.Metadata.Dataset,
                executed.Metadata.SemanticVersion,
                executed.Metadata.ExecutedAt,
                executed.Metadata.DataFreshnessAt,
                executed.Metadata.RowCount));

        return Ok(response);
    }

    private static IReadOnlyDictionary<string, object?> ToFlatRow(AnalyticsQueryRow row)
    {
        var flat = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in row.DimensionValues)
        {
            flat[key] = value;
        }

        foreach (var (key, value) in row.MetricValues)
        {
            flat[key] = value;
        }

        return flat;
    }
}
