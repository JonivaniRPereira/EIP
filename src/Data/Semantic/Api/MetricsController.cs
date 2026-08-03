using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Semantic.Api.Contracts;
using EIP.Data.Semantic.Application;
using EIP.Shared.Contracts.Metrics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Data.Semantic.Api;

/// <summary>
/// Camada semântica mínima (E6.2, docs/09-Data-Warehouse.md §9) — consulta das 3 métricas
/// certificadas da fatia Comercial. Nunca expõe tabelas do Warehouse diretamente (docs/09 §11:
/// "tabelas de DW não são expostas diretamente a navegadores ou clientes externos") — só os valores
/// agregados, sempre com a definição/dono/versão da métrica junto.
/// </summary>
[ApiController]
[Route("api/v1/metrics")]
public sealed class MetricsController : ControllerBase
{
    private readonly IMetricsQueryService _metricsQueryService;

    public MetricsController(IMetricsQueryService metricsQueryService)
    {
        _metricsQueryService = metricsQueryService;
    }

    [HttpGet("commercial")]
    [RequirePermission(MetricsPermissions.MetricsView)]
    public async Task<ActionResult<CommercialMetricsResponse>> GetCommercial(
        [FromQuery] Guid? companyId,
        [FromQuery] DateOnly? periodStart,
        [FromQuery] DateOnly? periodEnd,
        CancellationToken cancellationToken)
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var filter = new MetricsQueryFilter(tenantId, companyId, periodStart, periodEnd);
        var result = await _metricsQueryService.GetCommercialMetricsAsync(filter, cancellationToken);

        return Ok(new CommercialMetricsResponse(
            ToDto(result.NetRevenue),
            ToDto(result.InvoicedQuantity),
            ToDto(result.AverageTicket)));
    }

    private static CertifiedMetricValueDto ToDto(CertifiedMetricValue value) => new(
        value.Definition.Name,
        value.Definition.Description,
        value.Definition.Owner,
        value.Definition.Version,
        value.Value);
}
