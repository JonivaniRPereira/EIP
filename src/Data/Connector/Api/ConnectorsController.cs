using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Connector.Api.Contracts;
using EIP.Data.Connector.Application;
using EIP.Shared.Contracts.Connectors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Data.Connector.Api;

/// <summary>
/// Conector de referência da Fase 0 (E7): registra a instância REST genérica e dispara
/// sincronizações assíncronas. Nunca processa a sincronização de forma síncrona aqui — o corpo do
/// request só valida, persiste em <c>Pending</c> e publica a mensagem; o resultado é consultado
/// depois via <see cref="GetSyncRun"/> (docs/05-Connector-Framework.md §2).
/// </summary>
[ApiController]
[Route("api/v1/connectors")]
public sealed class ConnectorsController : ControllerBase
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly IConnectorSyncService _connectorSyncService;

    public ConnectorsController(IConnectorSyncService connectorSyncService)
    {
        _connectorSyncService = connectorSyncService;
    }

    [HttpPost]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<ActionResult<ConnectorInstanceDto>> Register(RegisterConnectorInstanceRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || !Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
        {
            return Problem(detail: "Name e BaseUrl (URL absoluta) são obrigatórios.", statusCode: StatusCodes.Status400BadRequest);
        }

        var instanceId = await _connectorSyncService.RegisterInstanceAsync(tenantId.Value, request.Name, request.BaseUrl, cancellationToken);

        var dto = new ConnectorInstanceDto(instanceId, request.Name, request.BaseUrl, "Active");
        return CreatedAtAction(nameof(Register), new { connectorInstanceId = instanceId }, dto);
    }

    /// <summary>Dispara uma sincronização assíncrona (E7.1/E7.2). Retorna 202 com o <c>SyncRunId</c>
    /// — o cliente consulta o progresso em <see cref="GetSyncRun"/>, nunca espera o resultado nesta
    /// chamada.</summary>
    [HttpPost("{connectorInstanceId:guid}/sync")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<IActionResult> RequestSync(Guid connectorInstanceId, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var correlationId = HttpContext.Items.TryGetValue(CorrelationIdHeaderName, out var value) && value is string existing
            ? existing
            : Guid.NewGuid().ToString();

        var result = await _connectorSyncService.RequestSyncAsync(connectorInstanceId, tenantId.Value, correlationId, cancellationToken);
        if (!result.Success)
        {
            return Problem(detail: result.Error, statusCode: StatusCodes.Status400BadRequest);
        }

        return AcceptedAtAction(nameof(GetSyncRun), new { connectorInstanceId, syncRunId = result.SyncRunId }, new { syncRunId = result.SyncRunId });
    }

    [HttpGet("{connectorInstanceId:guid}/sync-runs/{syncRunId:guid}")]
    [RequirePermission(ConnectorPermissions.ConnectorView)]
    public async Task<ActionResult<SyncRunDto>> GetSyncRun(Guid connectorInstanceId, Guid syncRunId, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var run = await _connectorSyncService.GetRunAsync(syncRunId, tenantId.Value, cancellationToken);
        if (run is null || run.ConnectorInstanceId != connectorInstanceId)
        {
            return NotFound();
        }

        return Ok(new SyncRunDto(
            run.Id,
            run.ConnectorInstanceId,
            run.Status.ToString(),
            run.CorrelationId,
            run.RecordsProcessed,
            run.ErrorMessage,
            run.CreatedAt,
            run.StartedAt,
            run.FinishedAt));
    }

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }
}
