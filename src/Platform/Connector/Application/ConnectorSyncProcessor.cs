using EIP.Platform.Connector.Application.Abstractions;
using EIP.Platform.Connector.Application.Contracts;

namespace EIP.Platform.Connector.Application;

public sealed class ConnectorSyncProcessor : IConnectorSyncProcessor
{
    private readonly IConnectorSyncStore _store;
    private readonly IReferenceRestClient _restClient;

    public ConnectorSyncProcessor(IConnectorSyncStore store, IReferenceRestClient restClient)
    {
        _store = store;
        _restClient = restClient;
    }

    public async Task ProcessAsync(SyncRequestedMessage message, CancellationToken cancellationToken)
    {
        var run = await _store.FindRunAsync(message.SyncRunId, cancellationToken);

        // Idempotência (docs/05-Connector-Framework.md §10.1): o RabbitMQ entrega pelo menos uma vez
        // (at-least-once) — uma reentrega de um run que já terminou ou já está em processamento não
        // é reprocessada. TryStartProcessing só avança Pending -> Running exatamente uma vez.
        if (run is null || run.IsTerminal || !run.TryStartProcessing())
        {
            return;
        }

        await _store.SaveRunAsync(run, cancellationToken);

        var instance = await _store.FindInstanceAsync(message.ConnectorInstanceId, cancellationToken);

        // O TenantId da mensagem nunca é aceito cegamente (docs/05 §12): o SESSION_CONTEXT já foi
        // definido a partir dele (worker), mas a instância retornada também precisa pertencer a esse
        // mesmo tenant — defesa em profundidade, igual ao lado da API (ConnectorSyncService).
        if (instance is null || instance.TenantId != message.TenantId)
        {
            run.Fail("Instância de conector não encontrada para o tenant informado na mensagem.");
            await _store.SaveRunAsync(run, cancellationToken);
            return;
        }

        try
        {
            var recordsProcessed = await _restClient.FetchRecordCountAsync(instance.BaseUrl, cancellationToken);
            run.Complete(recordsProcessed);
            await _store.SaveRunAsync(run, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            run.Fail(ex.Message);
            await _store.SaveRunAsync(run, cancellationToken);

            // Repassa para o consumidor RabbitMQ decidir nack -> DLQ (docs/05 §10.3) — o estado já
            // ficou auditável no SyncRun antes de propagar.
            throw;
        }
    }
}
