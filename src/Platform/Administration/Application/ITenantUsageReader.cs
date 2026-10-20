namespace EIP.Platform.Administration.Application;

public sealed record TenantUsageReport(Guid TenantId, DateTimeOffset From, DateTimeOffset To,
    long Synchronizations, long AnalyticsQueries, long DataLakeBytes, long DataLakeObjects,
    DateTimeOffset MeasuredAt);

public interface ITenantUsageReader
{
    Task<TenantUsageReport?> ReadAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
