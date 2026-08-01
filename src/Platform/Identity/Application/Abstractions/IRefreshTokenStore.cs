using EIP.Platform.Identity.Domain;

namespace EIP.Platform.Identity.Application.Abstractions;

public sealed record IssuedRefreshToken(string RawValue, DateTimeOffset ExpiresAtUtc);

public interface IRefreshTokenStore
{
    Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken);

    /// <summary>Retorna o token apenas se ele existir, estiver ativo (não revogado, não expirado) —
    /// nunca valida o token apenas verificando a existência da linha.</summary>
    Task<RefreshToken?> FindActiveByRawTokenAsync(string rawToken, CancellationToken cancellationToken);

    Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId, CancellationToken cancellationToken);
}
