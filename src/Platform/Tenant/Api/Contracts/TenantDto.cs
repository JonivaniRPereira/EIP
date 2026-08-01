namespace EIP.Platform.Tenant.Api.Contracts;

public sealed record TenantDto(Guid Id, string Name, string Slug, string Status);
