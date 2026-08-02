using System.Text.Json;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using RabbitMQ.Client;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Publica <see cref="SyncRequestedMessage"/> no exchange/fila descritos por
/// <see cref="ConnectorMessagingTopology"/>. Mantém uma única <see cref="IConnection"/> (reaberta se
/// cair) e abre um <see cref="IChannel"/> novo por publicação — canais do RabbitMQ.Client não são
/// seguros para uso concorrente entre threads sem sincronização externa, e o volume da Fase 0 não
/// justifica um pool de canais.
/// </summary>
public sealed class RabbitMqConnectorSyncPublisher : IConnectorSyncPublisher, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectorSyncPublisher(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task PublishAsync(SyncRequestedMessage message, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await ConnectorMessagingTopology.DeclareAsync(channel, cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = message.CorrelationId,
        };

        await channel.BasicPublishAsync(
            ConnectorMessagingTopology.ExchangeName,
            ConnectorMessagingTopology.SyncRequestedRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true })
        {
            return _connection;
        }

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            _connection = await new ConnectionFactory { Uri = new Uri(_connectionString) }.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
