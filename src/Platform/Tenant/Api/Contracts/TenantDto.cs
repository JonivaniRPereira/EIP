namespace EIP.Platform.Tenant.Api.Contracts;

public sealed record TenantDto(Guid Id, string Name, string Slug, string Status);

/// <summary><see cref="DefaultCurrency"/>/<see cref="DefaultTimezone"/> são opcionais — assume
/// "BRL"/"America/Sao_Paulo" quando omitidos (mesmos defaults já usados em todo dado de
/// demonstração/teste do projeto).</summary>
public sealed record CreateTenantRequest(string Name, string Slug, string CountryCode, string? DefaultCurrency, string? DefaultTimezone);

public sealed record CreateTenantResponse(Guid TenantId, string Name, string Slug, Guid CompanyId);
