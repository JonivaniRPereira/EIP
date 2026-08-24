using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.Connector.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EIP.Worker.Sync;

/// <summary>
/// Consome <see cref="SyncRequestedMessage"/> da fila declarada em
/// <see cref="ConnectorMessagingTopology"/> (E7.2). Cada entrega define o
/// <see cref="ITenantContextAccessor"/> ambiente a partir do <c>TenantId</c> da mensagem — o mesmo
/// papel que o middleware do Host cumpre a partir do claim JWT em cada request HTTP — antes de
/// delegar o processamento de fato a <see cref="IConnectorSyncProcessor"/>, que nunca conhece
/// RabbitMQ.
///
/// Falha de deserialização/versão de contrato → <c>BasicNack(requeue: false)</c> direto para a DLQ
/// (retentar não ajudaria, a mensagem nunca vai ficar válida). Falha no processamento (ex. timeout de
/// origem, erro transitório) → retry com backoff limitado via <see cref="_retryOptions"/> (E2.1 da
/// Fase 3): republica explicitamente na fila de retry correspondente à tentativa atual e reconhece a
/// entrega original, só indo para a DLQ depois de esgotar <see cref="ConnectorRetryOptions.MaxAttempts"/>
/// tentativas.
/// </summary>
public sealed partial class SyncRequestedConsumerService : BackgroundService
{
    private readonly RabbitMqConnectionOptions _options;
    private readonly ConnectorRetryOptions _retryOptions;
    private readonly IConnectorSyncProcessor _processor;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<SyncRequestedConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public SyncRequestedConsumerService(
        RabbitMqConnectionOptions options,
        ConnectorRetryOptions retryOptions,
        IConnectorSyncProcessor processor,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<SyncRequestedConsumerService> logger)
    {
        _options = options;
        _retryOptions = retryOptions;
        _processor = processor;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await new ConnectionFactory { Uri = new Uri(_options.ConnectionString) }.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await ConnectorMessagingTopology.DeclareAsync(_channel, _retryOptions, stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) => await HandleMessageAsync(ea, stoppingToken);

        await _channel.BasicConsumeAsync(
            ConnectorMessagingTopology.SyncRequestedQueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        LogConsuming(ConnectorMessagingTopology.SyncRequestedQueueName);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        SyncRequestedMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<SyncRequestedMessage>(ea.Body.Span);
        }
        catch (JsonException ex)
        {
            LogMalformedMessage(ex);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        if (message is null || message.ContractVersion != SyncRequestedMessage.CurrentContractVersion)
        {
            LogUnsupportedContractVersion(message?.ContractVersion);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
            return;
        }

        // SESSION_CONTEXT('TenantId') definido a partir do TenantId da mensagem (nunca aceito
        // cegamente — ConnectorSyncProcessor ainda valida que a instância pertence a este tenant),
        // igual ao middleware do Host a partir do claim JWT (docs/08-Multi-Tenant.md §5.1).
        _tenantContextAccessor.Current = new TenantContext(message.TenantId);

        try
        {
            await _processor.ProcessAsync(message, cancellationToken);
            await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var attempt = GetRetryAttempt(ea.BasicProperties);
            if (attempt < _retryOptions.MaxAttempts)
            {
                await PublishToRetryLevelAsync(ea, attempt, cancellationToken);
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
                LogProcessingFailedRetrying(ex, message.SyncRunId, attempt + 1);
            }
            else
            {
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                LogProcessingFailedExhausted(ex, message.SyncRunId);
            }
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task PublishToRetryLevelAsync(BasicDeliverEventArgs ea, int currentAttempt, CancellationToken cancellationToken)
    {
        var level = _retryOptions.Levels[currentAttempt];

        var headers = new Dictionary<string, object?>();
        if (ea.BasicProperties.Headers is { } existingHeaders)
        {
            foreach (var (key, value) in existingHeaders)
            {
                headers[key] = value;
            }
        }

        headers[ConnectorMessagingTopology.RetryAttemptHeaderName] = currentAttempt + 1;

        var properties = new BasicProperties
        {
            ContentType = ea.BasicProperties.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = ea.BasicProperties.CorrelationId,
            Headers = headers,
        };

        await _channel!.BasicPublishAsync(
            ConnectorMessagingTopology.RetryExchangeName,
            level.RoutingKey,
            mandatory: false,
            basicProperties: properties,
            body: ea.Body,
            cancellationToken: cancellationToken);
    }

    private static int GetRetryAttempt(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is { } headers &&
            headers.TryGetValue(ConnectorMessagingTopology.RetryAttemptHeaderName, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] bytes => int.Parse(System.Text.Encoding.UTF8.GetString(bytes), System.Globalization.CultureInfo.InvariantCulture),
                _ => 0,
            };
        }

        return 0;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Consumindo {Queue}...")]
    private partial void LogConsuming(string queue);

    [LoggerMessage(Level = LogLevel.Error, Message = "Mensagem de sincronização malformada — rejeitada para a DLQ.")]
    private partial void LogMalformedMessage(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Versão de contrato de sincronização não suportada: {ContractVersion}")]
    private partial void LogUnsupportedContractVersion(string? contractVersion);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Falha ao processar SyncRun {SyncRunId} — agendando tentativa {NextAttempt} com backoff.")]
    private partial void LogProcessingFailedRetrying(Exception exception, Guid syncRunId, int nextAttempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar SyncRun {SyncRunId} após esgotar as tentativas — enviado para a DLQ.")]
    private partial void LogProcessingFailedExhausted(Exception exception, Guid syncRunId);
}
