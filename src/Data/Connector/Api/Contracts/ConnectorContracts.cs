namespace EIP.Data.Connector.Api.Contracts;

public sealed record RegisterConnectorInstanceRequest(Guid CompanyId, string Name, string BaseUrl, string SourceEntity);
public sealed record UpdateConnectorConfigurationRequest(string Name, string BaseUrl);

public sealed record ConnectorInstanceDto(Guid Id, Guid CompanyId, string Name, string BaseUrl, string SourceEntity, string Status);

public sealed record SyncRunDto(
    Guid Id,
    Guid ConnectorInstanceId,
    string Status,
    string CorrelationId,
    int? RecordsProcessed,
    int? AcceptedCount,
    int? UpdatedCount,
    int? RejectedCount,
    int? DeletedCount,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

public sealed record ConnectorDlqMessageDto(
    Guid SyncRunId,
    Guid ConnectorInstanceId,
    string CorrelationId,
    string ContractVersion,
    string? FailureReason,
    DateTimeOffset? DeadLetteredAtUtc);

public sealed record QuarantineEntryDto(
    Guid Id,
    Guid ConnectorInstanceId,
    Guid SyncRunId,
    string SourceEntity,
    string RawObjectUri,
    string CorrelationId,
    string FailedRule,
    string Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt);
