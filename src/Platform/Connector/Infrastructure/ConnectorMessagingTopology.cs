using RabbitMQ.Client;

namespace EIP.Platform.Connector.Infrastructure;

/// <summary>
/// Topologia RabbitMQ do fluxo de sincronização (E7.2). Centralizada aqui (não duplicada entre
/// publisher e worker) porque declarar a mesma fila com argumentos diferentes em dois lugares faz o
/// RabbitMQ recusar a segunda declaração (PRECONDITION_FAILED) — publisher e worker chamam o mesmo
/// <see cref="DeclareAsync"/> ao abrir seu canal, e o primeiro a rodar cria tudo.
///
/// DLQ (docs/05-Connector-Framework.md §10.3): a fila principal declara
/// <c>x-dead-letter-exchange</c> vazio (exchange padrão) + <c>x-dead-letter-routing-key</c> igual ao
/// nome da fila morta — o exchange padrão roteia por routing key = nome da fila, então rejeitar uma
/// mensagem sem reencaminhar (<c>requeue: false</c>) a move automaticamente para a DLQ.
/// </summary>
public static class ConnectorMessagingTopology
{
    public const string ExchangeName = "eip.connector";
    public const string SyncRequestedQueueName = "connector.sync.requested";
    public const string SyncRequestedRoutingKey = "sync.requested";
    public const string SyncRequestedDeadLetterQueueName = "connector.sync.requested.dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            SyncRequestedDeadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: cancellationToken);

        var arguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = string.Empty,
            ["x-dead-letter-routing-key"] = SyncRequestedDeadLetterQueueName,
        };

        await channel.QueueDeclareAsync(
            SyncRequestedQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments,
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            SyncRequestedQueueName,
            ExchangeName,
            SyncRequestedRoutingKey,
            cancellationToken: cancellationToken);
    }
}
