using EIP.Platform.Connector.Domain;

namespace EIP.Platform.Connector.Application.Abstractions;

/// <summary>
/// Acesso a dados do módulo Connector. A implementação (Infrastructure) abre uma conexão nova por
/// operação via <c>IDbContextFactory</c> (mesmo padrão do <c>MembershipDirectory</c>/
/// <c>TenantsController</c> do módulo Tenant) — a RLS filtra as linhas automaticamente a partir do
/// <see cref="EIP.BuildingBlocks.Security.ITenantContextAccessor"/> ambiente, que já foi definido
/// pelo middleware do Host (a partir do claim JWT) ou explicitamente pelo worker (a partir do
/// <c>TenantId</c> da mensagem) antes de qualquer chamada aqui.
/// </summary>
public interface IConnectorSyncStore
{
    Task<ConnectorInstance?> FindInstanceAsync(Guid connectorInstanceId, CancellationToken cancellationToken);

    Task SaveNewInstanceAsync(ConnectorInstance instance, CancellationToken cancellationToken);

    Task SaveNewRunAsync(SyncRun run, CancellationToken cancellationToken);

    Task<SyncRun?> FindRunAsync(Guid syncRunId, CancellationToken cancellationToken);

    Task SaveRunAsync(SyncRun run, CancellationToken cancellationToken);
}
