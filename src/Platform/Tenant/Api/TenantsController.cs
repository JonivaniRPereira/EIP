using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Platform.Tenant.Api.Contracts;
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
    private readonly IDbContextFactory<TenantDbContext> _dbContextFactory;

    public TenantsController(IDbContextFactory<TenantDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
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
}
