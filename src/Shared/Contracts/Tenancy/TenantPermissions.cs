namespace EIP.Shared.Contracts.Tenancy;

/// <summary>Códigos de permissão usados nas claims do JWT e nos endpoints protegidos
/// (<c>[RequirePermission(...)]</c>). Lista mínima para o que existe na Fase 0 — cresce conforme
/// novos recursos/domínios forem expostos (docs/roadmap/fase-0-backlog.md, fora do escopo: Workspace).</summary>
public static class TenantPermissions
{
    public const string TenantView = "tenant.view";
    public const string TenantManage = "tenant.manage";
    public const string MembersView = "members.view";
    public const string MembersManage = "members.manage";
    public const string CompaniesView = "companies.view";
    public const string CompaniesManage = "companies.manage";
}
