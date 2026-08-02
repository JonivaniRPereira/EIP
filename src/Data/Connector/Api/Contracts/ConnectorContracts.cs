namespace EIP.Data.Connector.Api.Contracts;

public sealed record RegisterConnectorInstanceRequest(string Name, string BaseUrl);

public sealed record ConnectorInstanceDto(Guid Id, string Name, string BaseUrl, string Status);

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
