namespace EIP.Data.Catalog.Api.Contracts;

public sealed record LoadBatchLineageDto(Guid Id, string Status, string CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);

public sealed record CanonicalRecordLineageDto(Guid Id, string RawObjectUri, string SchemaVersion, string CorrelationId, DateTimeOffset IngestedAt, DateTimeOffset ProcessedAt);

public sealed record SyncRunLineageDto(Guid Id, string Status, string CorrelationId, DateTimeOffset CreatedAt, DateTimeOffset? FinishedAt);

public sealed record ConnectorInstanceLineageDto(Guid Id, string Name, string BaseUrl, string SourceEntity);

public sealed record DataLineageDto(
    int FactSalesInvoiceItemKey,
    Guid SourceSystemId,
    string SourceEntity,
    string SourceRecordId,
    string InvoiceNumber,
    int LineNumber,
    decimal NetAmount,
    string RawObjectUri,
    LoadBatchLineageDto? LoadBatch,
    CanonicalRecordLineageDto? CanonicalRecord,
    SyncRunLineageDto? SyncRun,
    ConnectorInstanceLineageDto? ConnectorInstance);

public sealed record MetricReconciliationSnapshotDto(
    Guid ConnectorInstanceId,
    string ConnectorInstanceName,
    bool IsWithinTolerance,
    int CanonicalCount,
    decimal CanonicalNetAmountTotal,
    int FactCount,
    decimal FactNetAmountTotal,
    string? Discrepancy);

public sealed record MetricCatalogEntryDto(
    string Name,
    string FormulaText,
    string Owner,
    string Version,
    IReadOnlyList<MetricReconciliationSnapshotDto> Reconciliations);
