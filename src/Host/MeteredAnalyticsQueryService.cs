using EIP.Intelligence.Analytics.Application;
using EIP.Shared.Contracts.Administration;

namespace EIP.Host;

public sealed class MeteredAnalyticsQueryService(DeclarativeAnalyticsQueryService inner, IAnalyticsUsageRecorder usage) : IDeclarativeAnalyticsQueryService
{
    public async Task<AnalyticsQueryExecutionResult> ExecuteAsync(Guid tenantId, AnalyticsQueryDefinition query, CancellationToken cancellationToken)
    {
        var result = await inner.ExecuteAsync(tenantId, query, cancellationToken);
        if (result.Success)
        {
            await usage.RecordAsync(tenantId, DateTimeOffset.UtcNow, cancellationToken);
        }

        return result;
    }
}
