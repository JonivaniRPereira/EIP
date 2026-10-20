using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Implementação de <see cref="IConnectorSyncStore"/> via <c>IDbContextFactory</c> (mesmo padrão do
/// <c>MembershipDirectory</c>/<c>TenantsController</c> do módulo Tenant): cada operação abre uma
/// conexão nova, garantindo que o <c>TenantSessionContextInterceptor</c> dispare com o
/// <see cref="EIP.BuildingBlocks.Security.ITenantContextAccessor"/> ambiente correto — nunca
/// reaproveitando uma conexão já aberta com outro TenantId.
/// </summary>
public sealed class ConnectorSyncStore : IConnectorSyncStore
{
    private readonly IDbContextFactory<ConnectorDbContext> _dbContextFactory;

    public ConnectorSyncStore(IDbContextFactory<ConnectorDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task<ConnectorInstance?> FindInstanceAsync(Guid connectorInstanceId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ConnectorInstances.SingleOrDefaultAsync(i => i.Id == connectorInstanceId, cancellationToken);
    }

    public async Task<bool> UpdateConfigurationAsync(Guid tenantId, Guid connectorInstanceId, string name, string baseUrl, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ConnectorInstances.Where(i => i.TenantId == tenantId && i.Id == connectorInstanceId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.Name, name).SetProperty(i => i.BaseUrl, baseUrl)
                .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken) == 1;
    }

    public async Task<IReadOnlyList<ConnectorInstance>> ListInstancesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.ConnectorInstances.Where(i => i.TenantId == tenantId).ToListAsync(cancellationToken);
    }

    public async Task SaveNewInstanceAsync(ConnectorInstance instance, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ConnectorInstances.Add(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveInstanceAsync(ConnectorInstance instance, CancellationToken cancellationToken)
    {
        if (instance.LastWatermark is not { } watermark)
        {
            return;
        }

        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        await dbContext.ConnectorInstances.Where(i => i.Id == instance.Id && i.TenantId == instance.TenantId
            && (i.LastWatermark == null || i.LastWatermark < watermark))
            .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.LastWatermark, watermark)
                .SetProperty(i => i.UpdatedAt, DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task SaveNewRunAsync(SyncRun run, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.SyncRuns.Add(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SyncRun?> FindRunAsync(Guid syncRunId, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.SyncRuns.SingleOrDefaultAsync(r => r.Id == syncRunId, cancellationToken);
    }

    public async Task SaveRunAsync(SyncRun run, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.SyncRuns.Update(run);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SyncRun>> ListRunsAsync(Guid tenantId, Guid connectorInstanceId, int limit, CancellationToken cancellationToken, DateTimeOffset? before = null, Guid? beforeId = null)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.SyncRuns.AsNoTracking().Where(r => r.TenantId == tenantId && r.ConnectorInstanceId == connectorInstanceId);
        if (before is { } date && beforeId is { } id)
        {
            query = query.Where(r => r.CreatedAt < date || (r.CreatedAt == date && r.Id.CompareTo(id) < 0));
        }
        return await query
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .Take(Math.Clamp(limit, 1, 100)).ToListAsync(cancellationToken);
    }

    public async Task<long> CountRunsAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.SyncRuns.LongCountAsync(r => r.TenantId == tenantId && r.CreatedAt >= from && r.CreatedAt < to, cancellationToken);
    }
}
