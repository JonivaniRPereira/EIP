using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.Analytics.Application;
using EIP.Intelligence.Dashboard.Api.Contracts;
using EIP.Intelligence.Dashboard.Domain;
using EIP.Intelligence.Dashboard.Infrastructure;
using EIP.Shared.Contracts.Dashboards;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EIP.Intelligence.Dashboard.Api;

/// <summary>
/// CRUD + publicação/arquivamento de dashboards (Fase 2, E3.1). Vai direto contra
/// <see cref="DashboardDbContext"/> via <see cref="IDbContextFactory{TContext}"/> — sem uma camada de
/// Application dedicada, mesmo padrão de <c>TenantsController</c>: a lógica de negócio real já vive
/// nos métodos de domínio (<c>Dashboard.Create</c>/<c>UpdateDraft</c>/<c>Publish</c>/<c>Archive</c>),
/// o controller só orquestra carregar → chamar método → salvar. Isolamento entre tenants nunca depende
/// de o controller lembrar de filtrar por <c>TenantId</c> — a RLS (ADR-007) já garante isso no banco;
/// um dashboard de outro tenant simplesmente não aparece nas consultas, mesmo adulterando o Id na rota.
///
/// Log estruturado (E5.3, fechamento da Fase 2, docs/15 §6: "publicação...e acesso por escopo são
/// auditados") em toda mutação e visualização — mesmo idioma de
/// <c>DeclarativeAnalyticsQueryService</c> (E1.6): nunca uma tabela de auditoria própria, o log
/// estruturado (Serilog, com CorrelationId automático via LogContext do Host) já cumpre esse papel,
/// mesma decisão já aceita para as consultas do Analytics Engine.
/// </summary>
[ApiController]
[Route("api/v1/dashboards")]
public sealed partial class DashboardsController : ControllerBase
{
    private const string SubjectClaimType = "sub";

    private readonly IDbContextFactory<DashboardDbContext> _dbContextFactory;
    private readonly IDeclarativeAnalyticsQueryService _analyticsQueryService;
    private readonly ILogger<DashboardsController> _logger;

    public DashboardsController(IDbContextFactory<DashboardDbContext> dbContextFactory, IDeclarativeAnalyticsQueryService analyticsQueryService, ILogger<DashboardsController> logger)
    {
        _dbContextFactory = dbContextFactory;
        _analyticsQueryService = analyticsQueryService;
        _logger = logger;
    }

    [HttpPost]
    [RequirePermission(DashboardPermissions.DashboardManage)]
    public async Task<ActionResult<DashboardDto>> Create(CreateDashboardRequest request, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        var ownerId = GetAuthenticatedUserId();
        if (tenantId is null || ownerId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        Domain.Dashboard dashboard;
        try
        {
            dashboard = Domain.Dashboard.Create(tenantId.Value, request.Name, ownerId.Value);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.Dashboards.Add(dashboard);
        await db.SaveChangesAsync(cancellationToken);

        LogDashboardAction("Created", dashboard.Id, tenantId.Value, ownerId.Value);
        return CreatedAtAction(nameof(GetById), new { id = dashboard.Id }, ToDto(dashboard));
    }

    [HttpGet]
    [RequirePermission(DashboardPermissions.DashboardView)]
    public async Task<ActionResult<IReadOnlyList<DashboardSummaryDto>>> List(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboards = await db.Dashboards
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new DashboardSummaryDto(d.Id, d.Name, d.Status.ToString(), d.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(dashboards);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(DashboardPermissions.DashboardView)]
    public async Task<ActionResult<DashboardDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboard = await db.Dashboards.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dashboard is null)
        {
            return NotFound();
        }

        var tenantId = GetAuthenticatedTenantId();
        var userId = GetAuthenticatedUserId();
        if (tenantId is not null && userId is not null)
        {
            LogDashboardAction("Viewed", dashboard.Id, tenantId.Value, userId.Value);
        }

        return Ok(ToDto(dashboard));
    }

    /// <summary>
    /// Executa a <c>QueryDefinition</c> de cada widget do dashboard via o Analytics Engine (E1.2) —
    /// nunca lê <c>warehouse.*</c> diretamente deste módulo (docs/00: `Intelligence` só consome
    /// contratos já publicados). Sempre consulta <see cref="Domain.Dashboard.Widgets"/> (o conteúdo
    /// atual, publicado ou rascunho — a mesma lista que <see cref="Domain.Dashboard.Publish"/>
    /// instantaneou pela última vez em <see cref="DashboardVersion"/>, quando existe), nunca relê o
    /// histórico de versões: para efeito de "o que este widget mostra agora", o rascunho e a última
    /// publicação são a mesma coisa assim que a publicação acontece — só divergem quando há edições
    /// não publicadas, caso em que mostrar o rascunho mais recente (em vez de uma versão obsoleta)
    /// é o comportamento correto para quem já tem permissão de ver o dashboard.
    /// </summary>
    [HttpPost("{id:guid}/query")]
    [RequirePermission(DashboardPermissions.DashboardView)]
    public async Task<ActionResult<IReadOnlyList<WidgetQueryResultDto>>> Query(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetAuthenticatedTenantId();
        if (tenantId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboard = await db.Dashboards.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dashboard is null)
        {
            return NotFound();
        }

        var results = new List<WidgetQueryResultDto>();
        foreach (var widget in dashboard.Widgets)
        {
            var executionResult = await _analyticsQueryService.ExecuteAsync(tenantId.Value, widget.QueryDefinition, cancellationToken);
            if (!executionResult.Success)
            {
                return Problem(detail: $"Widget '{widget.Title}': {executionResult.Error}", statusCode: StatusCodes.Status400BadRequest);
            }

            var executed = executionResult.Result!;
            results.Add(new WidgetQueryResultDto(
                widget.Title,
                executed.Data.Select(ToFlatRow).ToList(),
                new WidgetQueryMetadataDto(executed.Metadata.Dataset, executed.Metadata.SemanticVersion, executed.Metadata.ExecutedAt, executed.Metadata.DataFreshnessAt, executed.Metadata.RowCount)));
        }

        return Ok(results);
    }

    [HttpPatch("{id:guid}")]
    [RequirePermission(DashboardPermissions.DashboardManage)]
    public async Task<ActionResult<DashboardDto>> Update(Guid id, UpdateDashboardRequest request, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboard = await db.Dashboards.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dashboard is null)
        {
            return NotFound();
        }

        try
        {
            var widgets = request.Widgets?.Select(ToWidget).ToList();
            dashboard.UpdateDraft(request.Name, widgets);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        await db.SaveChangesAsync(cancellationToken);

        var tenantId = GetAuthenticatedTenantId();
        var userId = GetAuthenticatedUserId();
        if (tenantId is not null && userId is not null)
        {
            LogDashboardAction("Updated", dashboard.Id, tenantId.Value, userId.Value);
        }

        return Ok(ToDto(dashboard));
    }

    /// <summary>Publica o rascunho atual (docs/12 §4, E2.2) — Dashboard atualizado + nova
    /// DashboardVersion inserida no mesmo <see cref="DbContext.SaveChangesAsync(CancellationToken)"/>,
    /// atômico por construção (um único unit-of-work do EF Core).</summary>
    [HttpPost("{id:guid}/publish")]
    [RequirePermission(DashboardPermissions.DashboardManage)]
    public async Task<ActionResult<PublishDashboardResponse>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboard = await db.Dashboards.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dashboard is null)
        {
            return NotFound();
        }

        DashboardVersion version;
        try
        {
            version = dashboard.Publish(userId.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        db.DashboardVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);

        LogDashboardAction("Published", dashboard.Id, dashboard.TenantId, userId.Value);
        return Ok(new PublishDashboardResponse(dashboard.Id, version.Id, version.CreatedAt));
    }

    [HttpPost("{id:guid}/archive")]
    [RequirePermission(DashboardPermissions.DashboardManage)]
    public async Task<ActionResult<DashboardDto>> Archive(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Problem(detail: "Tenant não selecionado.", statusCode: StatusCodes.Status403Forbidden);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var dashboard = await db.Dashboards.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (dashboard is null)
        {
            return NotFound();
        }

        dashboard.Archive();
        await db.SaveChangesAsync(cancellationToken);

        LogDashboardAction("Archived", dashboard.Id, dashboard.TenantId, userId.Value);
        return Ok(ToDto(dashboard));
    }

    private static DashboardDto ToDto(Domain.Dashboard dashboard) => new(
        dashboard.Id,
        dashboard.Name,
        dashboard.OwnerId,
        dashboard.Status.ToString(),
        dashboard.Widgets.Select(ToWidgetDto).ToList(),
        dashboard.CreatedAt,
        dashboard.UpdatedAt);

    private static WidgetDto ToWidgetDto(Widget widget) => new(
        widget.Type.ToString(),
        widget.Title,
        widget.QueryDefinition,
        new WidgetVisualizationDto(widget.Visualization.Unit, widget.Visualization.Color));

    private static Widget ToWidget(WidgetRequest request)
    {
        if (!Enum.TryParse<WidgetType>(request.Type, ignoreCase: true, out var type))
        {
            throw new ArgumentException($"Tipo de widget '{request.Type}' inválido. Use Kpi, LineChart ou Table.", nameof(request));
        }

        var queryDefinitionRequest = request.QueryDefinition;
        var queryDefinition = new AnalyticsQueryDefinition(
            queryDefinitionRequest.Dataset ?? string.Empty,
            queryDefinitionRequest.Metrics ?? [],
            queryDefinitionRequest.Dimensions ?? [],
            (queryDefinitionRequest.Filters ?? [])
                .Select(f => new AnalyticsFilterClause(f.Field, f.Operator, f.Values ?? []))
                .ToList(),
            (queryDefinitionRequest.OrderBy ?? [])
                .Select(o => new AnalyticsOrderBy(o.Field, o.Direction))
                .ToList(),
            queryDefinitionRequest.Limit);

        return new Widget(
            type,
            request.Title,
            queryDefinition,
            new WidgetVisualization(request.Visualization?.Unit, request.Visualization?.Color));
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

    private Guid? GetAuthenticatedTenantId()
    {
        var tenantClaim = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : null;
    }

    private Guid? GetAuthenticatedUserId()
    {
        var subject = User.FindFirst(SubjectClaimType)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Dashboard {Action}: dashboardId={DashboardId} tenantId={TenantId} userId={UserId}")]
    private partial void LogDashboardAction(string action, Guid dashboardId, Guid tenantId, Guid userId);
}
