using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Domain;
using EIP.Data.Connector.Api.Contracts;
using EIP.Data.Connector.Application;
using EIP.Shared.Contracts.Connectors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Data.Connector.Api;

/// <summary>
/// Consulta e reprocessamento de quarentena (docs/04-Modelo-Canonico.md §8.2: "operador pode
/// corrigir o mapeamento e reprocessar a carga, mantendo trilha de auditoria", E4.2). Reprocessar
/// aqui dispara uma nova sincronização completa da instância dona da entrada — não existe
/// reprocessamento cirúrgico de um único registro nesta fase (o mapeamento é fixo no código, não
/// configurável, e o conteúdo bruto não é armazenado por registro individual, só por lote/SyncRun).
/// </summary>
[ApiController]
[Route("api/v1/connectors/quarantine")]
public sealed class QuarantineController : ControllerBase
{
    private readonly ICanonicalRecordStore _canonicalRecordStore;
    private readonly IConnectorSyncService _connectorSyncService;

    public QuarantineController(ICanonicalRecordStore canonicalRecordStore, IConnectorSyncService connectorSyncService)
    {
        _canonicalRecordStore = canonicalRecordStore;
        _connectorSyncService = connectorSyncService;
    }

    [HttpGet]
    [RequirePermission(ConnectorPermissions.ConnectorView)]
    public async Task<ActionResult<IReadOnlyList<QuarantineEntryDto>>> List(
        [FromQuery] Guid? connectorInstanceId,
        [FromQuery] DateTimeOffset? createdFrom,
        [FromQuery] DateTimeOffset? createdTo,
        CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var entries = await _canonicalRecordStore.ListQuarantineEntriesAsync(tenantId.Value, connectorInstanceId, createdFrom, createdTo, cancellationToken);

        return Ok(entries.Select(ToDto).ToList());
    }

    /// <summary>Dispara uma nova sincronização assíncrona (202, mesmo padrão de
    /// <see cref="ConnectorsController.RequestSync"/>) para a instância dona desta entrada, e marca a
    /// entrada como resolvida — a auditoria fica no <c>ResolvedAt</c> mais o novo <c>SyncRun</c>
    /// gerado; se a origem ainda estiver com problema, o novo lote gera uma nova entrada de
    /// quarentena, nunca reaproveita esta.</summary>
    [HttpPost("{quarantineEntryId:guid}/reprocess")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<IActionResult> Reprocess(Guid quarantineEntryId, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var entry = await _canonicalRecordStore.FindQuarantineEntryAsync(tenantId.Value, quarantineEntryId, cancellationToken);
        if (entry is null)
        {
            return NotFound();
        }

        var correlationId = HttpContext.Items.TryGetValue("X-Correlation-Id", out var value) && value is string existing
            ? existing
            : Guid.NewGuid().ToString();

        var result = await _connectorSyncService.RequestSyncAsync(entry.ConnectorInstanceId, tenantId.Value, correlationId, reprocessFromUtc: null, cancellationToken);
        if (!result.Success)
        {
            return Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        await _canonicalRecordStore.MarkQuarantineEntryResolvedAsync(quarantineEntryId, DateTimeOffset.UtcNow, cancellationToken);

        return Accepted(new { syncRunId = result.SyncRunId });
    }

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }

    private static QuarantineEntryDto ToDto(CanonicalQuarantineEntry entry) => new(
        entry.Id,
        entry.ConnectorInstanceId,
        entry.SyncRunId,
        entry.SourceEntity,
        entry.RawObjectUri,
        entry.CorrelationId,
        entry.FailedRule,
        entry.Reason,
        entry.CreatedAt,
        entry.ResolvedAt);
}
