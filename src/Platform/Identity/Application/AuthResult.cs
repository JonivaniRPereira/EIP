using EIP.Shared.Contracts.Tenancy;

namespace EIP.Platform.Identity.Application;

public sealed class AuthResult
{
    public bool Succeeded { get; }
    public string? Error { get; }
    public string? AccessToken { get; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    public string? RefreshToken { get; }
    public bool RequiresTenantSelection { get; }
    public IReadOnlyList<MembershipSummary> AvailableTenants { get; }

    private AuthResult(
        bool succeeded,
        string? error,
        string? accessToken,
        DateTimeOffset? accessTokenExpiresAtUtc,
        string? refreshToken,
        bool requiresTenantSelection,
        IReadOnlyList<MembershipSummary> availableTenants)
    {
        Succeeded = succeeded;
        Error = error;
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        RefreshToken = refreshToken;
        RequiresTenantSelection = requiresTenantSelection;
        AvailableTenants = availableTenants;
    }

    public static AuthResult Failed(string error) =>
        new(false, error, null, null, null, false, []);

    public static AuthResult Authenticated(string accessToken, DateTimeOffset expiresAtUtc, string refreshToken) =>
        new(true, null, accessToken, expiresAtUtc, refreshToken, false, []);

    public static AuthResult NeedsTenantSelection(
        string accessToken,
        DateTimeOffset expiresAtUtc,
        string refreshToken,
        IReadOnlyList<MembershipSummary> availableTenants) =>
        new(true, null, accessToken, expiresAtUtc, refreshToken, true, availableTenants);
}
