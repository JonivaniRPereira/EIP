using EIP.Platform.Connector.Application.Abstractions;
using EIP.Platform.Connector.Application.Contracts;
using EIP.Platform.Connector.Domain;

namespace EIP.Platform.Connector.Application;

public sealed class ConnectorSyncService : IConnectorSyncService
{
    private readonly IConnectorSyncStore _store;
    private readonly IConnectorSyncPublisher _publisher;

    public ConnectorSyncService(IConnectorSyncStore store, IConnectorSyncPublisher publisher)
    {
        _store = store;
        _publisher = publisher;
    }

    public async Task<Guid> RegisterInstanceAsync(Guid tenantId, string name, string baseUrl, CancellationToken cancellationToken)
    {
        var instance = ConnectorInstance.Create(tenantId, name, baseUrl);
        await _store.SaveNewInstanceAsync(instance, cancellationToken);
        return instance.Id;
    }

    public async Task<SyncRunRequestResult> RequestSyncAsync(Guid connectorInstanceId, Guid tenantId, string correlationId, CancellationToken cancellationToken)
    {
        var instance = await _store.FindInstanceAsync(connectorInstanceId, cancellationToken);

        // RLS já garante que só instâncias do tenant autenticado são visíveis (ADR-007); a
        // comparação abaixo é defesa em profundidade, mesmo padrão do TenantsController (E2.5) —
        // nunca a única barreira.
        if (instance is null || instance.TenantId != tenantId)
        {
            return SyncRunRequestResult.Failed("Instância de conector não encontrada.");
        }

        if (instance.Status != ConnectorInstanceStatus.Active)
        {
            return SyncRunRequestResult.Failed("Instância de conector não está ativa.");
        }

        var run = SyncRun.CreatePending(tenantId, instance.Id, correlationId);
        await _store.SaveNewRunAsync(run, cancellationToken);

        var message = new SyncRequestedMessage(
            run.Id,
            instance.Id,
            tenantId,
            correlationId,
            SyncRequestedMessage.CurrentContractVersion);

        try
        {
            await _publisher.PublishAsync(message, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem outbox transacional na Fase 0 (fica para hardening futuro): se a publicação falhar
            // depois do SyncRun já persistido, o run é marcado Failed em vez de ficar Pending para
            // sempre sem nenhuma mensagem jamais publicada.
            run.Fail($"Falha ao publicar mensagem de sincronização: {ex.Message}");
            await _store.SaveRunAsync(run, cancellationToken);
            return SyncRunRequestResult.Failed("Falha ao solicitar sincronização.");
        }

        return SyncRunRequestResult.Ok(run.Id);
    }

    public async Task<SyncRun?> GetRunAsync(Guid syncRunId, Guid tenantId, CancellationToken cancellationToken)
    {
        var run = await _store.FindRunAsync(syncRunId, cancellationToken);

        // Defesa em profundidade, igual ao RequestSyncAsync: RLS já restringe a visibilidade ao
        // tenant autenticado, mas o valor não é confiado sem essa comparação explícita.
        return run is not null && run.TenantId == tenantId ? run : null;
    }
}
