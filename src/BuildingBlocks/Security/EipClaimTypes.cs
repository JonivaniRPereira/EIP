namespace EIP.BuildingBlocks.Security;

/// <summary>Nomes de claim customizados compartilhados entre quem emite o JWT (Identity) e quem o
/// consome (middleware de tenant, autorização por permissão) — centralizados aqui para os dois
/// lados nunca divergirem.</summary>
public static class EipClaimTypes
{
    public const string TenantId = "tenant_id";

    /// <summary>Lista de permissões separadas por vírgula, resolvida a partir do papel da
    /// membership ativa no tenant selecionado (docs/08-Multi-Tenant.md §6).</summary>
    public const string Permissions = "permissions";
}
