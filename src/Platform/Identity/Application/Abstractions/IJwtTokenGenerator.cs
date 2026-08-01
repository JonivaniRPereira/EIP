namespace EIP.Platform.Identity.Application.Abstractions;

public sealed record JwtAccessToken(string Value, DateTimeOffset ExpiresAtUtc);

/// <summary>Gera o access token JWT. A implementação concreta (assinatura, issuer/audience) fica na
/// Infrastructure — a Application só depende desta abstração (docs/07-Seguranca.md §5.1).</summary>
public interface IJwtTokenGenerator
{
    /// <param name="tenantId">Nulo quando o usuário ainda não selecionou um tenant (login com
    /// múltiplas memberships ativas) — o token, nesse caso, só permite o endpoint de seleção de
    /// tenant (docs/08-Multi-Tenant.md §5.2).</param>
    JwtAccessToken GenerateAccessToken(Guid userId, string email, Guid? tenantId);
}
