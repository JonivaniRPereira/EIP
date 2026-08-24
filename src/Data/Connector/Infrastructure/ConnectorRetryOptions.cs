namespace EIP.Data.Connector.Infrastructure;

/// <summary>Um nível da escada de retry (E2.1 da Fase 3): uma fila RabbitMQ com TTL fixo que
/// devolve a mensagem para <see cref="ConnectorMessagingTopology.SyncRequestedQueueName"/> quando o
/// TTL expira.</summary>
public sealed record ConnectorRetryLevel(string QueueName, string RoutingKey, int DelayMilliseconds);

/// <summary>Configura a escada de retry usada por <see cref="ConnectorMessagingTopology.DeclareAsync"/>
/// e pelo worker. Injetável para permitir delays curtos em teste (a produção usa 10s/30s/90s, um
/// teste de integração não pode esperar 130s por um `Fact`) — os nomes de fila/routing key também são
/// configuráveis para que uma instância de teste nunca colida com a topologia de produção.</summary>
public sealed record ConnectorRetryOptions(IReadOnlyList<ConnectorRetryLevel> Levels)
{
    public int MaxAttempts => Levels.Count;

    public static ConnectorRetryOptions Default { get; } = new(
    [
        new ConnectorRetryLevel("connector.sync.retry.10s", "retry.10s", 10_000),
        new ConnectorRetryLevel("connector.sync.retry.30s", "retry.30s", 30_000),
        new ConnectorRetryLevel("connector.sync.retry.90s", "retry.90s", 90_000),
    ]);
}
