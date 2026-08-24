using EIP.Data.Canonical.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Warehouse.Application;

namespace EIP.Data.Catalog.Application;

/// <summary>
/// Implementação de <see cref="IDataLineageService"/> composta inteiramente a partir de stores de
/// outros módulos já existentes (nenhuma persistência própria) — o mesmo padrão de
/// <c>WarehouseReconciliationService</c> (Fase 0/1), que também depende de interfaces de Application
/// de outro módulo em vez de acessar o DbContext de outro módulo diretamente.
/// </summary>
public sealed class DataLineageService : IDataLineageService
{
    private readonly IWarehouseLoadStore _warehouseLoadStore;
    private readonly ICanonicalRecordStore _canonicalRecordStore;
    private readonly IConnectorSyncStore _connectorSyncStore;

    public DataLineageService(
        IWarehouseLoadStore warehouseLoadStore,
        ICanonicalRecordStore canonicalRecordStore,
        IConnectorSyncStore connectorSyncStore)
    {
        _warehouseLoadStore = warehouseLoadStore;
        _canonicalRecordStore = canonicalRecordStore;
        _connectorSyncStore = connectorSyncStore;
    }

    public async Task<DataLineageQueryResult> GetLineageAsync(Guid tenantId, int factSalesInvoiceItemKey, CancellationToken cancellationToken)
    {
        var fact = await _warehouseLoadStore.FindFactSalesInvoiceItemAsync(tenantId, factSalesInvoiceItemKey, cancellationToken);
        if (fact is null)
        {
            return DataLineageQueryResult.NotFound;
        }

        var loadBatch = await _warehouseLoadStore.FindLoadBatchAsync(tenantId, fact.LoadBatchId, cancellationToken);

        // Chave de negócio, nunca SalesInvoiceItemId (docs/04 §4.1) — o registro canônico pode ter
        // sido substituído (delete + insert) por um reprocessamento posterior da fatura.
        var canonicalRecord = await _canonicalRecordStore.FindSalesInvoiceItemByBusinessKeyAsync(
            tenantId, fact.SourceSystemId, fact.SourceEntity, fact.SourceRecordId, cancellationToken);

        var syncRunId = TryParseSyncRunIdFromRawObjectUri(fact.RawObjectUri);
        var syncRun = syncRunId is { } id ? await _connectorSyncStore.FindRunAsync(id, cancellationToken) : null;

        var connectorInstance = await _connectorSyncStore.FindInstanceAsync(fact.SourceSystemId, cancellationToken);

        var lineage = new DataLineage(
            fact.FactSalesInvoiceItemKey,
            fact.SourceSystemId,
            fact.SourceEntity,
            fact.SourceRecordId,
            fact.InvoiceNumber,
            fact.LineNumber,
            fact.NetAmount,
            fact.RawObjectUri,
            loadBatch is null ? null : new LoadBatchLineage(loadBatch.Id, loadBatch.Status.ToString(), loadBatch.CorrelationId, loadBatch.CreatedAt, loadBatch.FinishedAt),
            canonicalRecord is null ? null : new CanonicalRecordLineage(canonicalRecord.Id, canonicalRecord.RawObjectUri, canonicalRecord.SchemaVersion, canonicalRecord.CorrelationId, canonicalRecord.IngestedAt, canonicalRecord.ProcessedAt),
            syncRun is null ? null : new SyncRunLineage(syncRun.Id, syncRun.Status.ToString(), syncRun.CorrelationId, syncRun.CreatedAt, syncRun.FinishedAt),
            connectorInstance is null ? null : new ConnectorInstanceLineage(connectorInstance.Id, connectorInstance.Name, connectorInstance.BaseUrl, connectorInstance.SourceEntity));

        return new DataLineageQueryResult(DataLineageOutcome.Found, lineage);
    }

    /// <summary>O <c>RawObjectUri</c> é a própria chave do objeto no Data Lake, com o formato
    /// determinístico <c>{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequence}.json</c>
    /// (<c>S3RawObjectStore.BuildKey</c>) — o <c>SyncRunId</c> vem direto do 7º segmento, sem
    /// precisar de uma chamada ao Data Lake (que nem exporia isso: <c>IRawObjectStore</c> só devolve o
    /// conteúdo do objeto, não seus metadados). Objetos antigos sem sincronização associada usam o
    /// segmento literal <c>"no-sync-run"</c> — tratado como ausência, nunca um erro de parsing.</summary>
    private static Guid? TryParseSyncRunIdFromRawObjectUri(string rawObjectUri)
    {
        var segments = rawObjectUri.Split('/');
        return segments.Length > 6 && Guid.TryParse(segments[6], out var syncRunId) ? syncRunId : null;
    }
}
