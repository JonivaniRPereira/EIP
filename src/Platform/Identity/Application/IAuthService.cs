namespace EIP.Platform.Identity.Application;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken);

    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken);

    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);

    /// <param name="userId">Extraído do JWT já autenticado (claim sub) — nunca de input do cliente.</param>
    Task<AuthResult> SelectTenantAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken);
}
