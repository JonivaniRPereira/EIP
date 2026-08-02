using System.Security.Cryptography;
using System.Text;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EIP.Platform.Identity.Infrastructure;

/// <summary>
/// <c>identity.RefreshTokens</c> tem RLS obrigatória (ADR-007) apesar de o schema <c>identity</c> em
/// geral não ser tenant-scoped: a tabela guarda um <see cref="RefreshToken.TenantId"/> (nulo até o
/// tenant ser selecionado). Toda operação aqui roda sob a sentinela <see cref="TenantContext.System"/>
/// (mesmo bypass documentado usado por <c>MembershipDirectory</c> no login) porque a busca por hash
/// acontece necessariamente ANTES de qualquer tenant estar em contexto — não há como aplicar o filtro
/// real de tenant num lookup que existe justamente para descobrir/reemitir o tenant.
/// </summary>
public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AppIdentityDbContext _dbContext;
    private readonly JwtOptions _options;
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public RefreshTokenStore(AppIdentityDbContext dbContext, IOptions<JwtOptions> options, ITenantContextAccessor tenantContextAccessor)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _tenantContextAccessor = tenantContextAccessor;
    }

    public Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) =>
        RunWithSystemContextAsync(async () =>
        {
            var rawToken = GenerateRawToken();
            var hash = Hash(rawToken);
            var lifetime = TimeSpan.FromDays(_options.RefreshTokenLifetimeDays);

            var entity = RefreshToken.Create(userId, tenantId, hash, lifetime);
            _dbContext.RefreshTokens.Add(entity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new IssuedRefreshToken(rawToken, entity.ExpiresAt);
        });

    public Task<RefreshToken?> FindActiveByRawTokenAsync(string rawToken, CancellationToken cancellationToken) =>
        RunWithSystemContextAsync(async () =>
        {
            var hash = Hash(rawToken);
            var token = await _dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

            return token is not null && token.IsActive ? token : null;
        });

    public Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId, CancellationToken cancellationToken) =>
        RunWithSystemContextAsync(async () =>
        {
            token.Revoke(replacedByTokenId);
            await _dbContext.SaveChangesAsync(cancellationToken);
        });

    private async Task<T> RunWithSystemContextAsync<T>(Func<Task<T>> operation)
    {
        var previous = _tenantContextAccessor.Current;
        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            return await operation();
        }
        finally
        {
            _tenantContextAccessor.Current = previous;
        }
    }

    private async Task RunWithSystemContextAsync(Func<Task> operation)
    {
        var previous = _tenantContextAccessor.Current;
        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await operation();
        }
        finally
        {
            _tenantContextAccessor.Current = previous;
        }
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
