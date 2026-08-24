using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Platform.Tenant.Api.Contracts;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Tenant.Api;

[ApiController]
[Route("api/v1/tenants")]
public sealed class TenantsController : ControllerBase
{
    private const string SubjectClaimType = "sub";
    private const string DefaultCurrencyFallback = "BRL";
    private const string DefaultTimezoneFallback = "America/Sao_Paulo";

    private readonly IDbContextFactory<TenantDbContext> _dbContextFactory;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantsController(IDbContextFactory<TenantDbContext> dbContextFactory, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _tenantContextAccessor = tenantContextAccessor;
    }

    /// <summary>
    /// Provisionamento próprio de tenant (Fase 3, E1.1) — fecha a lacuna que forçou toda validação ao
    /// vivo desde a Fase 0 a criar tenants via SQL bruto sob a sentinela de sistema. Qualquer usuário
    /// autenticado pode chamar (sem exigir nenhuma permissão de tenant — o usuário ainda não tem
    /// nenhuma) graças ao <c>FallbackPolicy</c> global (E2.5, Fase 0): só autenticação, sem
    /// `[RequirePermission]` explícito. Cria Tenant + Company inicial + Membership `Owner` `Active`
    /// numa única transação (um só `SaveChangesAsync`) sob <see cref="TenantContext.System"/> — o
    /// próprio tenant recém-criado ainda não tem `SESSION_CONTEXT` nenhum definido para si mesmo
    /// antes de existir, então `Company`/`Membership` (RLS-protegidas) só conseguem ser inseridas sob
    /// a sentinela de sistema, nunca sob o próprio `TenantId` novo (que ainda não passou pelo
    /// middleware de tenant do Host, já que o token do cliente não tem `tenant_id` nenhum ainda).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CreateTenantResponse>> Create(CreateTenantRequest request, CancellationToken cancellationToken)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Name)
            || string.IsNullOrWhiteSpace(request.Slug)
            || string.IsNullOrWhiteSpace(request.CountryCode))
        {
            return Problem(detail: "Name, Slug e CountryCode são obrigatórios.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var checkDb = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await checkDb.Tenants.AnyAsync(t => t.Slug == request.Slug, cancellationToken))
        {
            return Problem(detail: $"Slug '{request.Slug}' já está em uso.", statusCode: StatusCodes.Status409Conflict);
        }

        Domain.Tenant tenant;
        Company company;
        Membership membership;
        try
        {
            tenant = Domain.Tenant.Create(request.Name, request.Slug, Guid.NewGuid(), request.DefaultTimezone ?? DefaultTimezoneFallback);
            tenant.Activate();
            company = Company.Create(tenant.Id, request.Name, request.DefaultCurrency ?? DefaultCurrencyFallback, request.CountryCode);
            membership = Membership.Create(userId.Value, tenant.Id, MembershipRole.Owner);
            membership.Activate();
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.Tenants.Add(tenant);
            db.Companies.Add(company);
            db.Memberships.Add(membership);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Corrida rara entre a checagem de unicidade acima e este INSERT — o índice único de
                // Slug (TenantConfiguration) é o backstop real; a checagem acima só evita a viagem ao
                // banco no caso comum.
                return Problem(detail: $"Slug '{request.Slug}' já está em uso.", statusCode: StatusCodes.Status409Conflict);
            }
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }

        return CreatedAtAction(nameof(GetById), new { tenantId = tenant.Id }, new CreateTenantResponse(tenant.Id, tenant.Name, tenant.Slug, company.Id));
    }

    /// <summary>Endpoint de referência para autorização por permissão + escopo de tenant (E2.5).
    /// O <paramref name="tenantId"/> vem da rota (input de cliente) e nunca é confiado sozinho: tem
    /// que bater com o claim de tenant do token já autenticado — RLS bloqueia no banco, isto
    /// bloqueia na API (defesa em profundidade, docs/08-Multi-Tenant.md §6).</summary>
    [HttpGet("{tenantId:guid}")]
    [RequirePermission(TenantPermissions.TenantView)]
    public async Task<ActionResult<TenantDto>> GetById(Guid tenantId, CancellationToken cancellationToken)
    {
        var claimTenantId = User.FindFirst(EipClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(claimTenantId, out var authenticatedTenantId) || authenticatedTenantId != tenantId)
        {
            return Problem(
                detail: "Tenant informado não corresponde ao tenant autenticado.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
        {
            return NotFound();
        }

        return Ok(new TenantDto(tenant.Id, tenant.Name, tenant.Slug, tenant.Status.ToString()));
    }

    private Guid? GetAuthenticatedUserId()
    {
        var subject = User.FindFirst(SubjectClaimType)?.Value;
        return Guid.TryParse(subject, out var userId) ? userId : null;
    }
}
