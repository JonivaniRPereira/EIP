using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.DataLake;
using EIP.Platform.Administration.Application;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Administration.Infrastructure;

/// <summary>Usado exclusivamente pela API administrativa autorizada. Opera sob o tenant alvo,
/// mantendo RLS ativa e restaurando o contexto original mesmo quando alguma dependência falha.</summary>
public sealed class TenantUsageReader(IDbContextFactory<UsageDbContext> factory, IConnectorSyncStore connectors,
    IRawStorageUsageReader storage, ITenantDirectory tenants, ITenantContextAccessor accessor) : ITenantUsageReader
{
    public async Task<TenantUsageReport?> ReadAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (await tenants.GetTenantAsync(tenantId, cancellationToken) is null)
        {
            return null;
        }

        var previous = accessor.Current;
        accessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            var analytics = await db.AnalyticsQueries.LongCountAsync(e => e.TenantId == tenantId && e.OccurredAt >= from && e.OccurredAt < to, cancellationToken);
            var syncs = await connectors.CountRunsAsync(tenantId, from, to, cancellationToken);
            var raw = await storage.ReadUsageAsync(tenantId, from, to, cancellationToken);
            return new TenantUsageReport(tenantId, from, to, syncs, analytics, raw.Bytes, raw.Objects, DateTimeOffset.UtcNow);
        }
        finally
        {
            accessor.Current = previous;
        }
    }
}
