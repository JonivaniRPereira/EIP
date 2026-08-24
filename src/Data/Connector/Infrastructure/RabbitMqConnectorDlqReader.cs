using System.Text;
using System.Text.Json;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using RabbitMQ.Client;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Lê/reprocessa a DLQ (E2.2 da Fase 3) via AMQP puro (<c>BasicGetAsync</c>), sem depender da API
/// HTTP de management do RabbitMQ — mesma escolha de dependência do resto do módulo (só
/// <c>RabbitMQ.Client</c>, nunca a management API, ver <see cref="RabbitMqConnectorSyncPublisher"/>).
///
/// Como não há como filtrar por tenant dentro do RabbitMQ, cada leitura navega (não-destrutivamente)
/// por até <see cref="MaxBrowseCount"/> mensagens da DLQ com <c>BasicGetAsync(autoAck: false)</c>,
/// devolve todas para a fila com <c>BasicNack(requeue: true)</c> ao final, e só então filtra por
/// tenant em memória — o volume esperado de uma DLQ operacional (dezenas, não milhões) torna isso
/// aceitável; não é um caminho de leitura de alto volume.
/// </summary>
public sealed class RabbitMqConnectorDlqReader : IConnectorDlqReader, IAsyncDisposable
{
    private const int MaxBrowseCount = 200;
    private const string FailureReasonHeaderKey = "reason";
    private const string FailureTimeHeaderKey = "time";

    private readonly string _connectionString;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectorDlqReader(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<DlqMessage>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await ConnectorMessagingTopology.DeclareAsync(channel, cancellationToken: cancellationToken);

        var browsed = await BrowseAsync(channel, cancellationToken);
        foreach (var entry in browsed)
        {
            await channel.BasicNackAsync(entry.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }

        return browsed
            .Select(entry => ParseMessage(entry))
            .Where(message => message is not null && message.TenantId == tenantId)
            .Select(message => new DlqMessage(
                message!.SyncRunId,
                message.ConnectorInstanceId,
                message.CorrelationId,
                message.ContractVersion,
                message.FailureReason,
                message.DeadLetteredAtUtc))
            .ToList();
    }

    public async Task<DlqRequeueOutcome> RequeueAsync(Guid tenantId, Guid syncRunId, CancellationToken cancellationToken)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await ConnectorMessagingTopology.DeclareAsync(channel, cancellationToken: cancellationToken);

        var browsed = await BrowseAsync(channel, cancellationToken);

        BrowsedEntry? matched = null;
        foreach (var entry in browsed)
        {
            if (matched is null)
            {
                var parsed = ParseMessage(entry);
                if (parsed is not null && parsed.SyncRunId == syncRunId && parsed.TenantId == tenantId)
                {
                    matched = entry;
                    continue;
                }
            }

            await channel.BasicNackAsync(entry.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }

        if (matched is null)
        {
            return DlqRequeueOutcome.NotFound;
        }

        // Remove definitivamente da DLQ (ack) e republica na fila principal para reprocessamento —
        // sem o header de tentativa (é um reprocessamento manual, não continuação da escada
        // automática de retry).
        await channel.BasicAckAsync(matched.DeliveryTag, multiple: false, cancellationToken);

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };

        await channel.BasicPublishAsync(
            ConnectorMessagingTopology.ExchangeName,
            ConnectorMessagingTopology.SyncRequestedRoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: matched.Body,
            cancellationToken: cancellationToken);

        return DlqRequeueOutcome.Requeued;
    }

    private static async Task<List<BrowsedEntry>> BrowseAsync(IChannel channel, CancellationToken cancellationToken)
    {
        var entries = new List<BrowsedEntry>();
        for (var i = 0; i < MaxBrowseCount; i++)
        {
            var result = await channel.BasicGetAsync(
                ConnectorMessagingTopology.SyncRequestedDeadLetterQueueName,
                autoAck: false,
                cancellationToken);

            if (result is null)
            {
                break;
            }

            entries.Add(new BrowsedEntry(result.DeliveryTag, result.Body.ToArray(), result.BasicProperties));
        }

        return entries;
    }

    private static ParsedDlqMessage? ParseMessage(BrowsedEntry entry)
    {
        SyncRequestedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SyncRequestedMessage>(entry.Body);
        }
        catch (JsonException)
        {
            return null;
        }

        if (message is null)
        {
            return null;
        }

        var (reason, deadLetteredAt) = ExtractDeathInfo(entry.Properties);

        return new ParsedDlqMessage(
            message.SyncRunId,
            message.ConnectorInstanceId,
            message.TenantId,
            message.CorrelationId,
            message.ContractVersion,
            reason,
            deadLetteredAt);
    }

    /// <summary>O broker anexa um header <c>x-death</c> (lista de tabelas AMQP) cada vez que
    /// dead-letra uma mensagem — o primeiro elemento é o evento mais recente, que é sempre o que
    /// efetivamente moveu a mensagem para a DLQ. Campos de string vêm como <c>byte[]</c> (encoding
    /// padrão de long-string do AMQP) quando não foram escritos pelo próprio cliente — nunca como
    /// <c>string</c>.</summary>
    private static (string? Reason, DateTimeOffset? DeadLetteredAtUtc) ExtractDeathInfo(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is not { } headers ||
            !headers.TryGetValue("x-death", out var xDeathValue) ||
            xDeathValue is not IList<object?> deaths ||
            deaths.Count == 0 ||
            deaths[0] is not IDictionary<string, object?> first)
        {
            return (null, null);
        }

        string? reason = first.TryGetValue(FailureReasonHeaderKey, out var reasonValue)
            ? ToDisplayString(reasonValue)
            : null;

        DateTimeOffset? time = first.TryGetValue(FailureTimeHeaderKey, out var timeValue) && timeValue is AmqpTimestamp timestamp
            ? DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime)
            : null;

        return (reason, time);
    }

    private static string? ToDisplayString(object? value) => value switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string text => text,
        _ => value?.ToString(),
    };

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

    private sealed record BrowsedEntry(ulong DeliveryTag, byte[] Body, IReadOnlyBasicProperties Properties);

    private sealed record ParsedDlqMessage(
        Guid SyncRunId,
        Guid ConnectorInstanceId,
        Guid TenantId,
        string CorrelationId,
        string ContractVersion,
        string? FailureReason,
        DateTimeOffset? DeadLetteredAtUtc);
}
