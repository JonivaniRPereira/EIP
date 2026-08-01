namespace EIP.Platform.Identity.Api.Contracts;

public sealed record RegisterRequestDto(string Email, string Password, string DisplayName);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record RefreshRequestDto(string RefreshToken);

public sealed record SelectTenantRequestDto(Guid TenantId);

public sealed record TenantOptionDto(Guid TenantId, string TenantName, string TenantSlug);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    bool RequiresTenantSelection,
    IReadOnlyList<TenantOptionDto> AvailableTenants);
