namespace EIP.Data.Catalog.Application;

/// <summary>Elo do Warehouse na cadeia de linhagem (Fase 3, E3.1).</summary>
public sealed record LoadBatchLineage(Guid Id, string Status, string CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);

/// <summary>Elo do Modelo Canônico. <see langword="null"/> quando o registro canônico já não existe
/// mais pela chave de negócio do fato (ex.: fatura reprocessada e os itens antigos substituídos,
/// docs/04 §4.1) — a cadeia continua sendo devolvida com os elos que ainda existem.</summary>
public sealed record CanonicalRecordLineage(Guid Id, string RawObjectUri, string SchemaVersion, string CorrelationId, DateTimeOffset IngestedAt, DateTimeOffset ProcessedAt);

/// <summary>Elo da sincronização que originou o dado. <see langword="null"/> quando o
/// <c>SyncRunId</c> não pôde ser determinado a partir do <c>RawObjectUri</c> (nunca deveria acontecer
/// para um dado que passou pelo fluxo normal do conector, mas o dado bruto mais antigo desta fase
/// pode não carregar essa informação).</summary>
public sealed record SyncRunLineage(Guid Id, string Status, string CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);

/// <summary>Elo da instância de conector de origem.</summary>
public sealed record ConnectorInstanceLineage(Guid Id, string Name, string BaseUrl, string SourceEntity);

/// <summary>Cadeia completa de rastreabilidade de um item de fato (Fase 3, E3.1, docs/04 §4/§8.3) —
/// do dado bruto no Data Lake até o fato analítico, reproduzindo em código o mesmo rastreamento feito
/// manualmente no fechamento da Fase 1 (E8.2, `mc stat` + consultas diretas). Cada elo depois do fato
/// em si pode faltar (nulo) sem que a consulta falhe — a cadeia é o que der para reconstruir a partir
/// do que está persistido agora, nunca uma garantia de que todo elo sempre existirá para sempre.</summary>
public sealed record DataLineage(
    int FactSalesInvoiceItemKey,
    Guid SourceSystemId,
    string SourceEntity,
    string SourceRecordId,
    string InvoiceNumber,
    int LineNumber,
    decimal NetAmount,
    string RawObjectUri,
    LoadBatchLineage? LoadBatch,
    CanonicalRecordLineage? CanonicalRecord,
    SyncRunLineage? SyncRun,
    ConnectorInstanceLineage? ConnectorInstance);

public enum DataLineageOutcome
{
    Found,
    NotFound,
}

public sealed record DataLineageQueryResult(DataLineageOutcome Outcome, DataLineage? Lineage)
{
    public static DataLineageQueryResult NotFound { get; } = new(DataLineageOutcome.NotFound, null);
}

/// <summary>
/// Formaliza como consulta o que até a Fase 3 só existia como prova manual (Fase 1, E8.2): rastrear
/// um item de fato do Warehouse até o dado bruto de origem. Nunca modifica nada — leitura pura,
/// composta a partir de interfaces já existentes de outros módulos (docs/02-Arquitetura.md §9.2: a
/// Application de um módulo pode depender da Application de outro, nunca da Infrastructure).
/// </summary>
public interface IDataLineageService
{
    Task<DataLineageQueryResult> GetLineageAsync(Guid tenantId, int factSalesInvoiceItemKey, CancellationToken cancellationToken);
}
