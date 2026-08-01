using EIP.BuildingBlocks.Security;
using EIP.Platform.Tenant.Domain;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Tenant.Infrastructure;

/// <summary>
/// Implementação do contrato cross-domain <see cref="IMembershipDirectory"/> (definido em
/// Shared/Contracts) consumido pelo domínio Identity durante o login — nunca o inverso (Identity
/// nunca acessa <see cref="TenantDbContext"/> diretamente).
///
/// Usa <see cref="IDbContextFactory{TenantDbContext}"/> em vez de um <see cref="TenantDbContext"/>
/// injetado por escopo: cada chamada cria um contexto novo, garantindo que a conexão seja aberta
/// (e portanto o <see cref="TenantSessionContextInterceptor"/> disparado) exatamente com a
/// sentinela de sistema definida — nunca reaproveitando uma conexão já aberta com o TenantId real
/// de uma operação anterior no mesmo request.
/// </summary>
public sealed class MembershipDirectory : IMembershipDirectory
{
    private readonly IDbContextFactory<TenantDbContext> _dbContextFactory;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public MembershipDirectory(IDbContextFactory<TenantDbContext> dbContextFactory, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContextFactory = dbContextFactory;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public Task<IReadOnlyList<MembershipSummary>> GetActiveMembershipsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return RunWithSystemContextAsync(async db =>
        {
            var memberships = await (
                from m in db.Memberships
                join t in db.Tenants on m.TenantId equals t.Id
                where m.UserId == userId && m.Status == MembershipStatus.Active
                select new MembershipSummary(t.Id, t.Name, t.Slug))
                .ToListAsync(cancellationToken);

            return (IReadOnlyList<MembershipSummary>)memberships;
        });
    }

    public Task<bool> HasActiveMembershipAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        return RunWithSystemContextAsync(db =>
            db.Memberships.AnyAsync(
                m => m.UserId == userId && m.TenantId == tenantId && m.Status == MembershipStatus.Active,
                cancellationToken));
    }

    private async Task<T> RunWithSystemContextAsync<T>(Func<TenantDbContext, Task<T>> operation)
    {
        var previous = _tenantContextAccessor.Current;
        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
            return await operation(dbContext);
        }
        finally
        {
            _tenantContextAccessor.Current = previous;
        }
    }
}
