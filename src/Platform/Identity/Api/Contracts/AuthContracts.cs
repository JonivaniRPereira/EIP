using System.ComponentModel.DataAnnotations;

namespace EIP.Platform.Identity.Api.Contracts;

public sealed record RegisterRequestDto(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(1024, MinimumLength = 8)] string Password,
    [Required, StringLength(200)] string DisplayName);

public sealed record LoginRequestDto(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(1024)] string Password);

public sealed record RefreshRequestDto([Required, StringLength(256)] string RefreshToken);

public sealed record SelectTenantRequestDto(Guid TenantId);

public sealed record TenantOptionDto(Guid TenantId, string TenantName, string TenantSlug);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    bool RequiresTenantSelection,
    IReadOnlyList<TenantOptionDto> AvailableTenants);
