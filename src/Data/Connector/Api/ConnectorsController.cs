using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Connector.Api.Contracts;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Shared.Contracts.Connectors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Data.Connector.Api;

/// <summary>
/// Conector de referência da Fase 0 (E7): registra a instância REST genérica e dispara
/// sincronizações assíncronas. Nunca processa a sincronização de forma síncrona aqui — o corpo do
/// request só valida, persiste em <c>Pending</c> e publica a mensagem; o resultado é consultado
/// depois via <see cref="GetSyncRun"/> (docs/05-Connector-Framework.md §2). A DLQ (<see cref="ListDlq"/>/
/// <see cref="RequeueDlq"/>, E2.2 da Fase 3) traz para dentro da plataforma o que antes só era visível
/// via RabbitMQ Management UI.
/// </summary>
[ApiController]
[Route("api/v1/connectors")]
public sealed class ConnectorsController : ControllerBase
{
    private const string CorrelationIdHeaderName = "X-Correlation-Id";

    private readonly IConnectorSyncService _connectorSyncService;
    private readonly IConnectorDlqReader _dlqReader;

    public ConnectorsController(IConnectorSyncService connectorSyncService, IConnectorDlqReader dlqReader)
    {
        _connectorSyncService = connectorSyncService;
        _dlqReader = dlqReader;
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

        if (string.IsNullOrWhiteSpace(request.Name)
            || !Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _)
            || string.IsNullOrWhiteSpace(request.SourceEntity)
            || request.CompanyId == Guid.Empty)
        {
            return Problem(
                detail: "CompanyId, Name, BaseUrl (URL absoluta) e SourceEntity são obrigatórios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var instanceId = await _connectorSyncService.RegisterInstanceAsync(
            tenantId.Value, request.CompanyId, request.Name, request.BaseUrl, request.SourceEntity, cancellationToken);

        var dto = new ConnectorInstanceDto(instanceId, request.CompanyId, request.Name, request.BaseUrl, request.SourceEntity, "Active");
        return CreatedAtAction(nameof(Register), new { connectorInstanceId = instanceId }, dto);
    }

    /// <summary>Dispara uma sincronização assíncrona (E7.1). Retorna 202 com o <c>SyncRunId</c>
    /// — o cliente consulta o progresso em <see cref="GetSyncRun"/>, nunca espera o resultado nesta
    /// chamada. <paramref name="reprocessFrom"/> (E7.2) reconstrói um período específico, ignorando
    /// o watermark salvo só para esta execução — nunca o avança depois.</summary>
    [HttpPost("{connectorInstanceId:guid}/sync")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<IActionResult> RequestSync(Guid connectorInstanceId, [FromQuery] DateTimeOffset? reprocessFrom, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var correlationId = HttpContext.Items.TryGetValue(CorrelationIdHeaderName, out var value) && value is string existing
            ? existing
            : Guid.NewGuid().ToString();

        var result = await _connectorSyncService.RequestSyncAsync(connectorInstanceId, tenantId.Value, correlationId, reprocessFrom, cancellationToken);
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
            run.AcceptedCount,
            run.UpdatedCount,
            run.RejectedCount,
            run.DeletedCount,
            run.ErrorMessage,
            run.CreatedAt,
            run.StartedAt,
            run.FinishedAt));
    }

    /// <summary>Lista as mensagens da DLQ que pertencem ao tenant autenticado (E2.2). A DLQ é um
    /// recurso físico único compartilhado por todos os tenants — o filtro por <c>TenantId</c>
    /// acontece dentro de <see cref="IConnectorDlqReader"/>, nunca aqui, para não depender de cada
    /// chamador lembrar de filtrar.</summary>
    [HttpGet("dlq")]
    [RequirePermission(ConnectorPermissions.ConnectorView)]
    public async Task<ActionResult<IReadOnlyList<ConnectorDlqMessageDto>>> ListDlq(CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var messages = await _dlqReader.ListAsync(tenantId.Value, cancellationToken);
        return Ok(messages
            .Select(message => new ConnectorDlqMessageDto(
                message.SyncRunId,
                message.ConnectorInstanceId,
                message.CorrelationId,
                message.ContractVersion,
                message.FailureReason,
                message.DeadLetteredAtUtc))
            .ToList());
    }

    /// <summary>Reprocessa manualmente uma mensagem da DLQ (E2.2). <paramref name="syncRunId"/> de
    /// outro tenant devolve 404 — mesmo padrão IDOR-safe de <see cref="GetSyncRun"/> (nunca revela que
    /// a mensagem existe, só não é sua).</summary>
    [HttpPost("dlq/{syncRunId:guid}/requeue")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<IActionResult> RequeueDlq(Guid syncRunId, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var outcome = await _dlqReader.RequeueAsync(tenantId.Value, syncRunId, cancellationToken);
        if (outcome == DlqRequeueOutcome.NotFound)
        {
            return NotFound();
        }

        return NoContent();
    }

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }
}
