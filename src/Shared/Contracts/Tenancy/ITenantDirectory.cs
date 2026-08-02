namespace EIP.Shared.Contracts.Tenancy;

public sealed record TenantSummary(Guid TenantId, string Name);

public sealed record CompanySummary(Guid CompanyId, string Name, string CountryCode, string DefaultCurrency);

/// <summary>
/// Contrato de comunicação entre domínios (docs/02-Arquitetura.md §9.2), mesmo padrão de
/// <see cref="IMembershipDirectory"/>: o domínio Warehouse (E5) nunca acessa a persistência do
/// domínio Tenant diretamente para resolver os atributos de <c>DimTenant</c>/<c>DimCompany</c>
/// (docs/09-Data-Warehouse.md §5.2) — depende só desta abstração, implementada pelo módulo Tenant.
/// </summary>
public interface ITenantDirectory
{
    Task<TenantSummary?> GetTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<CompanySummary?> GetCompanyAsync(Guid tenantId, Guid companyId, CancellationToken cancellationToken);
}
