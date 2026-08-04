using EIP.Data.Canonical.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.DataLake;
using EIP.Data.Pipeline;
using EIP.Data.Warehouse.Application;
using EIP.Shared.Contracts.Canonical;
using Microsoft.Extensions.Logging;

namespace EIP.Data.Connector.Application;

public sealed partial class ConnectorSyncProcessor : IConnectorSyncProcessor
{
    // Limite de divergência aceitável nas reconciliações (docs/04 §8.3, docs/09 §8.2) — fixo nesta
    // fase; parametrização por tenant/conector fica para quando houver demanda real (Fase 2).
    private const decimal ReconciliationToleranceFraction = 0.01m;

    private readonly IConnectorSyncStore _store;
    private readonly IReferenceRestClient _restClient;
    private readonly IRawObjectStore _rawObjectStore;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly ICanonicalReconciliationService _reconciliationService;
    private readonly IWarehouseLoadService _warehouseLoadService;
    private readonly IWarehouseReconciliationService _warehouseReconciliationService;
    private readonly ILogger<ConnectorSyncProcessor> _logger;

    public ConnectorSyncProcessor(
        IConnectorSyncStore store,
        IReferenceRestClient restClient,
        IRawObjectStore rawObjectStore,
        IPipelineProcessor pipelineProcessor,
        ICanonicalReconciliationService reconciliationService,
        IWarehouseLoadService warehouseLoadService,
        IWarehouseReconciliationService warehouseReconciliationService,
        ILogger<ConnectorSyncProcessor> logger)
    {
        _store = store;
        _restClient = restClient;
        _rawObjectStore = rawObjectStore;
        _pipelineProcessor = pipelineProcessor;
        _reconciliationService = reconciliationService;
        _warehouseLoadService = warehouseLoadService;
        _warehouseReconciliationService = warehouseReconciliationService;
        _logger = logger;
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
            // Capturado ANTES da extração (E7.1, docs/04 §11): se o watermark fosse gravado só
            // depois, um registro atualizado na origem entre o início da extração e o fim do
            // processamento seria perdido na próxima sincronização incremental.
            var extractionStartedAt = DateTimeOffset.UtcNow;

            // Reprocessamento manual por período (E7.2) usa a data explícita da mensagem, ignorando
            // o watermark salvo — nunca o contrário, para não pular um período nunca sincronizado.
            var updatedSince = message.ReprocessFromUtc ?? instance.LastWatermark;

            var rawContent = await _restClient.FetchRawContentAsync(instance.BaseUrl, updatedSince, cancellationToken);

            var metadata = new RawObjectMetadata(
                instance.TenantId,
                SourceSystemId: instance.Id,
                instance.SourceEntity,
                ConnectorInstanceId: instance.Id,
                SyncRunId: run.Id,
                IngestedAt: DateTimeOffset.UtcNow);
            var stored = await _rawObjectStore.PutAsync(metadata, rawContent, cancellationToken);

            var pipelineRequest = new PipelineProcessingRequest(
                instance.TenantId,
                instance.CompanyId,
                SourceSystemId: instance.Id,
                SyncRunId: run.Id,
                instance.SourceEntity,
                message.CorrelationId,
                RawObjectUri: stored.Key,
                RawContent: rawContent);
            var result = await _pipelineProcessor.ProcessAsync(pipelineRequest, cancellationToken);

            run.Complete(result.ExtractedCount, result.AcceptedCount, result.UpdatedCount, result.RejectedCount, result.DeletedCount);
            await _store.SaveRunAsync(run, cancellationToken);

            // Só avança o watermark automático em sincronizações normais — um reprocessamento manual
            // por período (E7.2) nunca move a cadência automática para frente, já que pode cobrir só
            // uma janela do passado sem garantia de continuidade até agora.
            if (message.ReprocessFromUtc is null)
            {
                instance.AdvanceWatermark(extractionStartedAt);
                await _store.SaveInstanceAsync(instance, cancellationToken);
            }

            // Reconciliação Canônico↔Origem (docs/04 §8.3, E4.3) — só para sales-invoices, onde
            // "totais por período/origem" fazem sentido de negócio. Nunca falha o SyncRun por conta
            // disso: o bloqueio automático de publicação fica para E5/E6, aqui só registra o alerta.
            if (instance.SourceEntity == CanonicalSourceEntities.SalesInvoices && result.NetAmountTotal is { } netAmountTotal)
            {
                var reconciliation = await _reconciliationService.ReconcileSalesInvoicesAsync(
                    instance.TenantId,
                    instance.Id,
                    result.AcceptedCount,
                    netAmountTotal,
                    ReconciliationToleranceFraction,
                    cancellationToken);

                if (!reconciliation.IsWithinTolerance)
                {
                    LogReconciliationOutOfTolerance(run.Id, reconciliation.Discrepancy);
                }

                // Carga do Warehouse (E5.3) — sempre a partir do Modelo Canônico já validado acima,
                // nunca direto da origem (docs/09 §7.1).
                await _warehouseLoadService.LoadSalesInvoiceItemsAsync(instance.TenantId, instance.Id, message.CorrelationId, cancellationToken);

                // Reconciliação Canônico↔Fato (docs/09 §8.2, E5.4) — mesmo padrão não bloqueante da
                // reconciliação Canônico↔Origem acima.
                var warehouseReconciliation = await _warehouseReconciliationService.ReconcileSalesInvoiceItemsAsync(
                    instance.TenantId, instance.Id, ReconciliationToleranceFraction, cancellationToken);

                if (!warehouseReconciliation.IsWithinTolerance)
                {
                    LogWarehouseReconciliationOutOfTolerance(run.Id, warehouseReconciliation.Discrepancy);
                }
            }
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciliação Canônico↔Origem fora da tolerância para SyncRun {SyncRunId}: {Discrepancy}")]
    private partial void LogReconciliationOutOfTolerance(Guid syncRunId, string? discrepancy);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciliação Canônico↔Fato fora da tolerância para SyncRun {SyncRunId}: {Discrepancy}")]
    private partial void LogWarehouseReconciliationOutOfTolerance(Guid syncRunId, string? discrepancy);
}
