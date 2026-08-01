using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Identity.Application.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EIP.Platform.Identity.Infrastructure;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;

    public JwtTokenGenerator(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public JwtAccessToken GenerateAccessToken(Guid userId, string email, Guid? tenantId, IReadOnlyCollection<string> permissions)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (tenantId is not null)
        {
            claims.Add(new Claim(EipClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        if (permissions.Count > 0)
        {
            claims.Add(new Claim(EipClaimTypes.Permissions, string.Join(',', permissions)));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);

        return new JwtAccessToken(value, expiresAtUtc);
    }
}
