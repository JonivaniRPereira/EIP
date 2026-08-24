using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Catalog.Api.Contracts;
using EIP.Data.Catalog.Application;
using EIP.Shared.Contracts.Dashboards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Data.Catalog.Api;

/// <summary>
/// Catálogo de Dados e Linhagem (Fase 3, E3) — formaliza como endpoint o que até aqui só existia como
/// prova manual em sessões de validação (Fase 1, E8.2). Protegido por <c>dashboard.view</c> em vez de
/// uma permissão nova: linhagem/metadados de métrica são parte de "confiar no dado" consumido pelo
/// Dashboard Builder/Analytics Engine, não uma capacidade separada.
/// </summary>
[ApiController]
[Route("api/v1/data-catalog")]
[RequirePermission(DashboardPermissions.DashboardView)]
public sealed class DataCatalogController : ControllerBase
{
    private readonly IDataLineageService _lineageService;
    private readonly IMetricsCatalogService _metricsCatalogService;

    public DataCatalogController(IDataLineageService lineageService, IMetricsCatalogService metricsCatalogService)
    {
        _lineageService = lineageService;
        _metricsCatalogService = metricsCatalogService;
    }

    [HttpGet("lineage/{factSalesInvoiceItemId:int}")]
    public async Task<ActionResult<DataLineageDto>> GetLineage(int factSalesInvoiceItemId, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await _lineageService.GetLineageAsync(tenantId.Value, factSalesInvoiceItemId, cancellationToken);
        if (result.Outcome == DataLineageOutcome.NotFound)
        {
            return NotFound();
        }

        var lineage = result.Lineage!;
        return Ok(new DataLineageDto(
            lineage.FactSalesInvoiceItemKey,
            lineage.SourceSystemId,
            lineage.SourceEntity,
            lineage.SourceRecordId,
            lineage.InvoiceNumber,
            lineage.LineNumber,
            lineage.NetAmount,
            lineage.RawObjectUri,
            lineage.LoadBatch is { } loadBatch ? new LoadBatchLineageDto(loadBatch.Id, loadBatch.Status, loadBatch.CorrelationId, loadBatch.CreatedAt, loadBatch.FinishedAt) : null,
            lineage.CanonicalRecord is { } canonicalRecord ? new CanonicalRecordLineageDto(canonicalRecord.Id, canonicalRecord.RawObjectUri, canonicalRecord.SchemaVersion, canonicalRecord.CorrelationId, canonicalRecord.IngestedAt, canonicalRecord.ProcessedAt) : null,
            lineage.SyncRun is { } syncRun ? new SyncRunLineageDto(syncRun.Id, syncRun.Status, syncRun.CorrelationId, syncRun.CreatedAt, syncRun.FinishedAt) : null,
            lineage.ConnectorInstance is { } connectorInstance ? new ConnectorInstanceLineageDto(connectorInstance.Id, connectorInstance.Name, connectorInstance.BaseUrl, connectorInstance.SourceEntity) : null));
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<IReadOnlyList<MetricCatalogEntryDto>>> ListMetrics(CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var metrics = await _metricsCatalogService.ListMetricsAsync(tenantId.Value, cancellationToken);
        return Ok(metrics
            .Select(m => new MetricCatalogEntryDto(
                m.Name,
                m.FormulaText,
                m.Owner,
                m.Version,
                m.Reconciliations
                    .Select(r => new MetricReconciliationSnapshotDto(
                        r.ConnectorInstanceId, r.ConnectorInstanceName, r.IsWithinTolerance,
                        r.CanonicalCount, r.CanonicalNetAmountTotal, r.FactCount, r.FactNetAmountTotal, r.Discrepancy))
                    .ToList()))
            .ToList());
    }

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }
}
