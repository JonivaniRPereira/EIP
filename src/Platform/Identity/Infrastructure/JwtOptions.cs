namespace EIP.Platform.Identity.Infrastructure;

/// <summary>Vinculado à seção "Jwt" da configuração. A chave de assinatura em desenvolvimento local
/// é um valor "dev only" (nunca um segredo real) — em produção deve vir de um cofre de segredos
/// (docs/07-Seguranca.md §8).</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public required string Issuer { get; init; }

    public required string Audience { get; init; }

    public required string SigningKey { get; init; }

    /// <summary>Curta duração por design (docs/07-Seguranca.md §5.1).</summary>
    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 7;
}
