namespace EIP.Shared.Contracts.Tenancy;

public sealed record MembershipSummary(Guid TenantId, string TenantName, string TenantSlug);
