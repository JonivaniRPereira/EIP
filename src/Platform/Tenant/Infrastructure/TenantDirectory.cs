using EIP.Shared.Contracts.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Tenant.Infrastructure;

/// <summary>
/// Implementação do contrato cross-domain <see cref="ITenantDirectory"/> (definido em
/// Shared/Contracts), consumido pelo processo de carga do Warehouse (E5.3) para resolver os
/// atributos de <c>DimTenant</c>/<c>DimCompany</c>. Ao contrário de <see cref="MembershipDirectory"/>,
/// não usa a sentinela de sistema: o chamador (Worker) já está operando sob o
/// <c>SESSION_CONTEXT</c> do tenant correto durante toda a sincronização, então uma consulta comum
/// já é corretamente filtrada pela RLS de <c>tenant.Companies</c>.
/// </summary>
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly IDbContextFactory<TenantDbContext> _dbContextFactory;

    public TenantDirectory(IDbContextFactory<TenantDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<TenantSummary?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var tenant = await db.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        return tenant is null ? null : new TenantSummary(tenant.Id, tenant.Name);
    }

    public async Task<CompanySummary?> GetCompanyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var company = await db.Companies.SingleOrDefaultAsync(c => c.TenantId == tenantId && c.Id == companyId, cancellationToken);
        return company is null ? null : new CompanySummary(company.Id, company.Name, company.CountryCode, company.DefaultCurrency);
    }
}
