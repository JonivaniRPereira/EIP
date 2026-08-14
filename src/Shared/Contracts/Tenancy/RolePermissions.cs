using EIP.Shared.Contracts.Analytics;
using EIP.Shared.Contracts.Connectors;
using EIP.Shared.Contracts.Metrics;

namespace EIP.Shared.Contracts.Tenancy;

/// <summary>
/// Mapeamento papel→permissões (RBAC simplificado para o MVP — docs/08-Multi-Tenant.md §6). Vive em
/// Shared porque o domínio Identity precisa resolvê-lo ao emitir o JWT sem depender do domínio
/// Tenant (docs/02-Arquitetura.md §9.2); os nomes de papel (string) espelham o enum
/// <c>EIP.Platform.Tenant.Domain.MembershipRole</c>.
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, IReadOnlySet<string>> Map = new(StringComparer.Ordinal)
    {
        ["Owner"] = new HashSet<string>
        {
            TenantPermissions.TenantView,
            TenantPermissions.TenantManage,
            TenantPermissions.MembersView,
            TenantPermissions.MembersManage,
            TenantPermissions.CompaniesView,
            TenantPermissions.CompaniesManage,
            ConnectorPermissions.ConnectorView,
            ConnectorPermissions.ConnectorManage,
            MetricsPermissions.MetricsView,
            AnalyticsPermissions.AnalyticsQuery,
        },
        ["Admin"] = new HashSet<string>
        {
            TenantPermissions.TenantView,
            TenantPermissions.MembersView,
            TenantPermissions.MembersManage,
            TenantPermissions.CompaniesView,
            TenantPermissions.CompaniesManage,
            ConnectorPermissions.ConnectorView,
            ConnectorPermissions.ConnectorManage,
            MetricsPermissions.MetricsView,
            AnalyticsPermissions.AnalyticsQuery,
        },
        ["Member"] = new HashSet<string>
        {
            TenantPermissions.TenantView,
            TenantPermissions.CompaniesView,
            ConnectorPermissions.ConnectorView,
            MetricsPermissions.MetricsView,
            AnalyticsPermissions.AnalyticsQuery,
        },
    };

    /// <summary>Papel desconhecido resolve para nenhuma permissão (negar por padrão), nunca lança.</summary>
    public static IReadOnlySet<string> Resolve(string role) =>
        Map.TryGetValue(role, out var permissions) ? permissions : new HashSet<string>();
}
