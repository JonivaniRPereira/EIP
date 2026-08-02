namespace EIP.Shared.Contracts.Connectors;

/// <summary>Códigos de permissão do módulo Connector (E7), mesma convenção de
/// <c>EIP.Shared.Contracts.Tenancy.TenantPermissions</c> — usados nas claims do JWT e em
/// <c>[RequirePermission(...)]</c>.</summary>
public static class ConnectorPermissions
{
    public const string ConnectorView = "connector.view";
    public const string ConnectorManage = "connector.manage";
}
