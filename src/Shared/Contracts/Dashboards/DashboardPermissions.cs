namespace EIP.Shared.Contracts.Dashboards;

/// <summary>Códigos de permissão do Dashboard Builder (Fase 2, E3.3), mesma convenção de
/// <c>EIP.Shared.Contracts.Connectors.ConnectorPermissions</c> — usados nas claims do JWT e em
/// <c>[RequirePermission(...)]</c>. Owner/Admin ganham view+manage; Member ganha só view.</summary>
public static class DashboardPermissions
{
    public const string DashboardView = "dashboard.view";
    public const string DashboardManage = "dashboard.manage";
}
