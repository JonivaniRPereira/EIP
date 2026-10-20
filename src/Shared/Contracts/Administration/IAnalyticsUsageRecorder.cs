namespace EIP.Shared.Contracts.Administration;

public interface IAnalyticsUsageRecorder
{
    Task RecordAsync(Guid tenantId, DateTimeOffset occurredAt, CancellationToken cancellationToken);
}
