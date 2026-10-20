using EIP.Platform.Identity.Domain;

namespace EIP.Platform.Identity.Application.Abstractions;

public sealed record IssuedRefreshToken(string RawValue, DateTimeOffset ExpiresAtUtc);

public interface IRefreshTokenStore
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken);

    /// <summary>Consome atomicamente o token ativo. Só uma requisição concorrente pode resgatá-lo;
    /// falha após o consumo exige novo login, sem reutilizar o token anterior.</summary>
    Task<RefreshToken?> TryRedeemAsync(string rawToken, CancellationToken cancellationToken);

    Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId, CancellationToken cancellationToken);
}
