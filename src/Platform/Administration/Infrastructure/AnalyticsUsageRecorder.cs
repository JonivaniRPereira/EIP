using EIP.Shared.Contracts.Administration;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Administration.Infrastructure;

public sealed class AnalyticsUsageRecorder(IDbContextFactory<UsageDbContext> factory) : IAnalyticsUsageRecorder
{
    public async Task RecordAsync(Guid tenantId, DateTimeOffset occurredAt, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.AnalyticsQueries.Add(new AnalyticsUsageEvent { Id = Guid.NewGuid(), TenantId = tenantId, OccurredAt = occurredAt });
        await db.SaveChangesAsync(cancellationToken);
    }
}
