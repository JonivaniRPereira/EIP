namespace EIP.Data.Connector.Api.Contracts;

public sealed record RegisterConnectorInstanceRequest(Guid CompanyId, string Name, string BaseUrl, string SourceEntity);

public sealed record ConnectorInstanceDto(Guid Id, Guid CompanyId, string Name, string BaseUrl, string SourceEntity, string Status);

public sealed record SyncRunDto(
    Guid Id,
    Guid ConnectorInstanceId,
    string Status,
    string CorrelationId,
    int? RecordsProcessed,
    string? ErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);
