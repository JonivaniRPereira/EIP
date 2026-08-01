using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.AspNetCore.Identity;

namespace EIP.Platform.Identity.Application;

public sealed class AuthService : IAuthService
{
    private const string InvalidCredentialsError = "Credenciais inválidas.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IRefreshTokenStore _refreshTokenStore;
    private readonly IMembershipDirectory _membershipDirectory;
    private readonly IAuditLogger _auditLogger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator tokenGenerator,
        IRefreshTokenStore refreshTokenStore,
        IMembershipDirectory membershipDirectory,
        IAuditLogger auditLogger)
    {
        _userManager = userManager;
        _tokenGenerator = tokenGenerator;
        _refreshTokenStore = refreshTokenStore;
        _membershipDirectory = membershipDirectory;
        _auditLogger = auditLogger;
    }

    public async Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Mensagem genérica: não confirma nem nega existência da conta para quem não é o dono.
            return AuthResult.Failed(InvalidCredentialsError);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
        };

        var createResult = await _userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var error = string.Join(" ", createResult.Errors.Select(e => e.Description));
            return AuthResult.Failed(error);
        }

        await _auditLogger.LogAsync(AuditEventTypes.UserRegistered, user.Id, email, detail: null, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            await _auditLogger.LogAsync(AuditEventTypes.LoginFailed, userId: null, email, "usuário não encontrado", cancellationToken);
            return AuthResult.Failed(InvalidCredentialsError);
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            await _auditLogger.LogAsync(AuditEventTypes.LoginLockedOut, user.Id, email, detail: null, cancellationToken);
            return AuthResult.Failed("Conta temporariamente bloqueada por tentativas inválidas.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, password);
        if (!passwordValid)
        {
            await _userManager.AccessFailedAsync(user);
            await _auditLogger.LogAsync(AuditEventTypes.LoginFailed, user.Id, email, "senha incorreta", cancellationToken);
            return AuthResult.Failed(InvalidCredentialsError);
        }

        await _userManager.ResetAccessFailedCountAsync(user);
        await _auditLogger.LogAsync(AuditEventTypes.LoginSucceeded, user.Id, email, detail: null, cancellationToken);

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var existingToken = await _refreshTokenStore.FindActiveByRawTokenAsync(refreshToken, cancellationToken);
        if (existingToken is null)
        {
            return AuthResult.Failed("Refresh token inválido ou expirado.");
        }

        var user = await _userManager.FindByIdAsync(existingToken.UserId.ToString());
        if (user is null)
        {
            return AuthResult.Failed("Refresh token inválido ou expirado.");
        }

        // Permissões são resolvidas de novo (não copiadas do token antigo): se a membership foi
        // alterada/revogada desde a emissão original, o refresh reflete o estado atual —
        // "negar por padrão" também se aplica aqui (docs/07-Seguranca.md §5.2).
        IReadOnlyCollection<string> permissions = Array.Empty<string>();
        if (existingToken.TenantId is { } tenantId)
        {
            var membership = await _membershipDirectory.GetActiveMembershipAsync(user.Id, tenantId, cancellationToken);
            if (membership is null)
            {
                await _refreshTokenStore.RevokeAsync(existingToken, null, cancellationToken);
                return AuthResult.Failed("Membership não é mais ativa neste tenant; faça login novamente.");
            }

            permissions = RolePermissions.Resolve(membership.Role);
        }

        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email!, existingToken.TenantId, permissions);
        var newRefreshToken = await _refreshTokenStore.IssueAsync(user.Id, existingToken.TenantId, cancellationToken);

        // Rotação: o refresh token usado é revogado e substituído pelo novo (docs/07-Seguranca.md §5.1).
        await _refreshTokenStore.RevokeAsync(existingToken, null, cancellationToken);

        return AuthResult.Authenticated(accessToken.Value, accessToken.ExpiresAtUtc, newRefreshToken.RawValue);
    }

    public async Task<AuthResult> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var membership = await _membershipDirectory.GetActiveMembershipAsync(userId, tenantId, cancellationToken);
        if (membership is null)
        {
            return AuthResult.Failed("Usuário não possui membership ativa neste tenant.");
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return AuthResult.Failed("Usuário não encontrado.");
        }

        var permissions = RolePermissions.Resolve(membership.Role);
        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email!, tenantId, permissions);
        var refreshToken = await _refreshTokenStore.IssueAsync(user.Id, tenantId, cancellationToken);

        return AuthResult.Authenticated(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.RawValue);
    }

    private async Task<AuthResult> IssueTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var memberships = await _membershipDirectory.GetActiveMembershipsAsync(user.Id, cancellationToken);

        // Exatamente uma membership ativa: seleciona automaticamente. Zero ou mais de uma: a troca
        // de tenant deve ser explícita (docs/08-Multi-Tenant.md §5.2) — o token sai sem claim de
        // tenant e o cliente deve chamar /select-tenant.
        var selected = memberships.Count == 1 ? memberships[0] : null;
        IReadOnlyCollection<string> permissions = selected is not null ? RolePermissions.Resolve(selected.Role) : Array.Empty<string>();

        var accessToken = _tokenGenerator.GenerateAccessToken(user.Id, user.Email!, selected?.TenantId, permissions);
        var refreshToken = await _refreshTokenStore.IssueAsync(user.Id, selected?.TenantId, cancellationToken);

        return selected is not null
            ? AuthResult.Authenticated(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.RawValue)
            : AuthResult.NeedsTenantSelection(accessToken.Value, accessToken.ExpiresAtUtc, refreshToken.RawValue, memberships);
    }
}
