using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Data.Connector.Api.Contracts;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Shared.Contracts.Connectors;
using EIP.Shared.Contracts.Tenancy;
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
    private readonly ITenantDirectory _tenants;
    private readonly IReferenceEndpointPolicy _endpointPolicy;

    public ConnectorsController(IConnectorSyncService connectorSyncService, IConnectorDlqReader dlqReader,
        ITenantDirectory tenants, IReferenceEndpointPolicy endpointPolicy)
    {
        _connectorSyncService = connectorSyncService;
        _dlqReader = dlqReader;
        _tenants = tenants;
        _endpointPolicy = endpointPolicy;
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
            || request.SourceEntity is not ("customers" or "products" or "sales-invoices")
            || request.CompanyId == Guid.Empty)
        {
            return Problem(
                detail: "CompanyId, Name, BaseUrl (URL absoluta) e SourceEntity são obrigatórios.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!_endpointPolicy.IsAllowed(request.BaseUrl))
        {
            return Problem(detail: "Destino de conector não autorizado pelo operador.", statusCode: StatusCodes.Status400BadRequest);
        }

        if (await _tenants.GetCompanyAsync(tenantId.Value, request.CompanyId, cancellationToken) is null)
        {
            return Problem(detail: "Empresa não encontrada nesta organização.", statusCode: StatusCodes.Status400BadRequest);
        }

        var instanceId = await _connectorSyncService.RegisterInstanceAsync(
            tenantId.Value, request.CompanyId, request.Name, request.BaseUrl, request.SourceEntity, cancellationToken);

        var dto = new ConnectorInstanceDto(instanceId, request.CompanyId, request.Name, request.BaseUrl, request.SourceEntity, "Active");
        return CreatedAtAction(nameof(Register), new { connectorInstanceId = instanceId }, dto);
    }

    [HttpGet]
    [RequirePermission(ConnectorPermissions.ConnectorView)]
    public async Task<ActionResult<IReadOnlyList<ConnectorInstanceDto>>> List(CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }
        var instances = await _connectorSyncService.ListInstancesAsync(tenantId.Value, cancellationToken);
        return Ok(instances.OrderBy(i => i.Name).ThenBy(i => i.Id)
            .Select(i => new ConnectorInstanceDto(i.Id, i.CompanyId, i.Name, i.BaseUrl, i.SourceEntity, i.Status.ToString())).ToList());
    }

    [HttpPut("{connectorInstanceId:guid}/configuration")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<ActionResult<ConnectorInstanceDto>> UpdateConfiguration(Guid connectorInstanceId,
        UpdateConnectorConfigurationRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(request.BaseUrl) || request.BaseUrl.Length > 2000
            || !_endpointPolicy.IsAllowed(request.BaseUrl))
        {
            return BadRequest(new ProblemDetails { Detail = "Informe nome de até 200 caracteres e endereço autorizado pelo administrador.", Status = 400 });
        }

        var instance = await _connectorSyncService.UpdateConfigurationAsync(tenantId.Value, connectorInstanceId,
            request.Name.Trim(), request.BaseUrl.Trim(), cancellationToken);
        if (instance is null)
        {
            return NotFound();
        }

        return Ok(new ConnectorInstanceDto(instance.Id, instance.CompanyId, instance.Name, instance.BaseUrl, instance.SourceEntity, instance.Status.ToString()));
    }

    [HttpGet("{connectorInstanceId:guid}/sync-runs")]
    [RequirePermission(ConnectorPermissions.ConnectorView)]
    public async Task<ActionResult<IReadOnlyList<SyncRunDto>>> ListRuns(Guid connectorInstanceId,
        CancellationToken cancellationToken, [FromQuery] int limit = 50, [FromQuery] DateTimeOffset? before = null, [FromQuery] Guid? beforeId = null)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Forbid();
        }
        if (limit is < 1 or > 100)
        {
            return BadRequest(new ProblemDetails { Detail = "Limite deve estar entre 1 e 100.", Status = 400 });
        }
        if (before.HasValue != beforeId.HasValue || beforeId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails { Detail = "Informe data e identificador válidos para continuar o histórico.", Status = 400 });
        }
        var runs = await _connectorSyncService.ListRunsAsync(tenantId.Value, connectorInstanceId, limit, cancellationToken, before, beforeId);
        if (runs is null)
        {
            return NotFound();
        }
        return Ok(runs.Select(r => new SyncRunDto(r.Id, r.ConnectorInstanceId, r.Status.ToString(), r.CorrelationId,
            r.RecordsProcessed, r.AcceptedCount, r.UpdatedCount, r.RejectedCount, r.DeletedCount,
            r.ErrorMessage, r.CreatedAt, r.StartedAt, r.FinishedAt)).ToList());
    }

    /// <summary>Dispara uma sincronização assíncrona (E7.1). Retorna 202 com o <c>SyncRunId</c>
    /// — o cliente consulta o progresso em <see cref="GetSyncRun"/>, nunca espera o resultado nesta
    /// chamada. <paramref name="reprocessFrom"/> (E7.2) reconstrói um período específico, ignorando
    /// o watermark salvo só para esta execução — nunca o avança depois.</summary>
    [HttpPost("{connectorInstanceId:guid}/sync")]
    [RequirePermission(ConnectorPermissions.ConnectorManage)]
    public async Task<IActionResult> RequestSync(Guid connectorInstanceId, [FromQuery] DateTimeOffset? reprocessFrom, CancellationToken cancellationToken)
    {
        if (reprocessFrom > DateTimeOffset.UtcNow)
        {
            return BadRequest(new ProblemDetails { Detail = "A data inicial não pode estar no futuro.", Status = 400 });
        }

        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        var correlationId = HttpContext.Items.TryGetValue(CorrelationIdHeaderName, out var value) && value is string existing
            ? existing
            : Guid.NewGuid().ToString();

        var result = await _connectorSyncService.RequestSyncAsync(connectorInstanceId, tenantId.Value, correlationId, reprocessFrom, cancellationToken);
        if (result.RetryAfter is { } retryAfter)
        {
            Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Problem(detail: result.Error, statusCode: StatusCodes.Status429TooManyRequests);
        }

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
