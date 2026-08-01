using System.Security.Cryptography;
using System.Text;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EIP.Platform.Identity.Infrastructure;

public sealed class RefreshTokenStore : IRefreshTokenStore
{
    private readonly AppIdentityDbContext _dbContext;
    private readonly JwtOptions _options;

    public RefreshTokenStore(AppIdentityDbContext dbContext, IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async Task<IssuedRefreshToken> IssueAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var hash = Hash(rawToken);
        var lifetime = TimeSpan.FromDays(_options.RefreshTokenLifetimeDays);

        var entity = RefreshToken.Create(userId, tenantId, hash, lifetime);
        _dbContext.RefreshTokens.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(rawToken, entity.ExpiresAt);
    }

    public async Task<RefreshToken?> FindActiveByRawTokenAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        var token = await _dbContext.RefreshTokens.SingleOrDefaultAsync(rt => rt.TokenHash == hash, cancellationToken);

        return token is not null && token.IsActive ? token : null;
    }

    public async Task RevokeAsync(RefreshToken token, Guid? replacedByTokenId, CancellationToken cancellationToken)
    {
        token.Revoke(replacedByTokenId);
        await _dbContext.SaveChangesAsync(cancellationToken);
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
