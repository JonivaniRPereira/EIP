using RabbitMQ.Client;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Topologia RabbitMQ do fluxo de sincronização (E7.2, retry ampliado na E2.1 da Fase 3). Centralizada
/// aqui (não duplicada entre publisher e worker) porque declarar a mesma fila com argumentos
/// diferentes em dois lugares faz o RabbitMQ recusar a segunda declaração (PRECONDITION_FAILED) —
/// publisher e worker chamam o mesmo <see cref="DeclareAsync"/> ao abrir seu canal, e o primeiro a
/// rodar cria tudo.
///
/// DLQ (docs/05-Connector-Framework.md §10.3): a fila principal declara
/// <c>x-dead-letter-exchange</c> vazio (exchange padrão) + <c>x-dead-letter-routing-key</c> igual ao
/// nome da fila morta — o exchange padrão roteia por routing key = nome da fila, então rejeitar uma
/// mensagem sem reencaminhar (<c>requeue: false</c>) a move automaticamente para a DLQ.
///
/// Retry com backoff (E2.1): sem o plugin <c>x-delayed-message</c> (decisão da Fase 0 E7.2 — não
/// instalado na imagem <c>rabbitmq:3.13-management-alpine</c>), o atraso é implementado com uma
/// escada de filas de retry (<see cref="ConnectorRetryOptions"/>), cada uma com <c>x-message-ttl</c>
/// fixo e <c>x-dead-letter-*</c> apontando de volta para a fila principal — quando o TTL expira, a
/// mensagem volta sozinha para <see cref="SyncRequestedQueueName"/> para nova tentativa. O worker
/// republica explicitamente (não usa Nack automático) na fila de retry correta, incrementando o
/// header <see cref="RetryAttemptHeaderName"/> a cada tentativa — só depois de esgotar
/// <see cref="ConnectorRetryOptions.MaxAttempts"/> tentativas é que a mensagem vai para a DLQ de fato.
/// </summary>
public static class ConnectorMessagingTopology
{
    public const string ExchangeName = "eip.connector";
    public const string SyncRequestedQueueName = "connector.sync.requested";
    public const string SyncRequestedRoutingKey = "sync.requested";
    public const string SyncRequestedDeadLetterQueueName = "connector.sync.requested.dlq";

    public const string RetryExchangeName = "eip.connector.retry";
    public const string RetryAttemptHeaderName = "x-retry-attempt";

    public static async Task DeclareAsync(
        IChannel channel,
        ConnectorRetryOptions? retryOptions = null,
        CancellationToken cancellationToken = default)
    {
        retryOptions ??= ConnectorRetryOptions.Default;

        await channel.ExchangeDeclareAsync(
            ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            RetryExchangeName,
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

        foreach (var level in retryOptions.Levels)
        {
            var retryArguments = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = level.DelayMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = SyncRequestedQueueName,
            };

            await channel.QueueDeclareAsync(
                level.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: retryArguments,
                cancellationToken: cancellationToken);

            await channel.QueueBindAsync(
                level.QueueName,
                RetryExchangeName,
                level.RoutingKey,
                cancellationToken: cancellationToken);
        }
    }
}
