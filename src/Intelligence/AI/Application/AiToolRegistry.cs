using System.Text.Json;
using EIP.Intelligence.Analytics.Application;
using EIP.Shared.Contracts.Analytics;

namespace EIP.Intelligence.AI.Application;

public sealed record AiToolDefinition(
    string Name, string Description, string RequiredPermission, string Scope,
    string Risk, bool ReadOnly, bool RequiresConfirmation, int MaxInputBytes, JsonElement InputSchema);

public sealed record AiCatalogField(string Name, string DisplayName, string Description);
public sealed record AiDataset(string Name, string Owner, string SemanticVersion,
    IReadOnlyList<AiCatalogField> Metrics, IReadOnlyList<AiCatalogField> Dimensions);
public sealed record AiDiscoveryResult(Guid TenantId, string Tool, DateTimeOffset ExecutedAt,
    IReadOnlyList<AiDataset> Datasets);

/// <summary>Allowlist inicial de ferramentas; sem SQL, HTTP arbitrário ou acesso a provedor.</summary>
public static class AiToolRegistry
{
    public const string ListDatasets = "listDatasets";
    public const int MaxInputBytes = 4096;

    public static IReadOnlyList<AiToolDefinition> Tools { get; } = Array.AsReadOnly<AiToolDefinition>([
        new(ListDatasets, "Lista datasets e definições de métricas autorizadas, sem consultar registros.",
            AnalyticsPermissions.AnalyticsQuery, "authenticatedTenant", "low", true, false, MaxInputBytes,
            JsonSerializer.Deserialize<JsonElement>("""{"type":"object","properties":{},"additionalProperties":false}""")),
    ]);

    public static AiDiscoveryResult Discover(Guid tenantId) => new(tenantId, ListDatasets, DateTimeOffset.UtcNow,
        AnalyticsCatalog.All.Select(dataset => new AiDataset(
            dataset.Name, dataset.Owner, dataset.SemanticVersion,
            dataset.Metrics.Values.Select(m => new AiCatalogField(m.Name, m.DisplayName, m.Description)).ToList(),
            dataset.Dimensions.Values.Select(d => new AiCatalogField(d.Name, d.DisplayName, d.Description)).ToList())).ToList());
}
