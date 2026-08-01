using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Identity.Domain;

/// <summary>
/// Refresh token com rotação e revogação (docs/07-Seguranca.md §5.1). Armazena apenas o hash do
/// token — o valor bruto nunca é persistido, só devolvido uma vez ao cliente na emissão.
/// </summary>
public sealed class RefreshToken : Entity<Guid>
{
    public Guid UserId { get; private set; }

    /// <summary>Tenant ativo no momento da emissão (nulo se o login ainda não selecionou um tenant).
    /// Permite reemitir o access token no /refresh com o mesmo claim, sem exigir nova seleção de
    /// tenant a cada expiração de 15 minutos.</summary>
    public Guid? TenantId { get; private set; }

    public string TokenHash { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    private RefreshToken(Guid id, Guid userId, Guid? tenantId, string tokenHash, DateTimeOffset expiresAt)
        : base(id)
    {
        UserId = userId;
        TenantId = tenantId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private RefreshToken()
    {
        TokenHash = string.Empty;
    }

    public static RefreshToken Create(Guid userId, Guid? tenantId, string tokenHash, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        return new RefreshToken(Guid.NewGuid(), userId, tenantId, tokenHash, DateTimeOffset.UtcNow.Add(lifetime));
    }

    /// <summary>Revoga o token, opcionalmente registrando qual novo token o substituiu (rotação).</summary>
    public void Revoke(Guid? replacedByTokenId = null)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
