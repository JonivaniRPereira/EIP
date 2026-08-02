using EIP.Platform.Connector.Application.Abstractions;
using EIP.Platform.Connector.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Connector.Infrastructure;

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

    public async Task SaveNewInstanceAsync(ConnectorInstance instance, CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.ConnectorInstances.Add(instance);
        await dbContext.SaveChangesAsync(cancellationToken);
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
}
