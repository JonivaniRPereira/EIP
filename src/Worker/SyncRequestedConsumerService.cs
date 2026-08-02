using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Connector.Application;
using EIP.Platform.Connector.Application.Contracts;
using EIP.Platform.Connector.Infrastructure;
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
/// Falha no processamento → <c>BasicNack(requeue: false)</c>: a fila principal tem
/// <c>x-dead-letter-*</c> configurado, então a mensagem cai direto na DLQ (sem retry com backoff —
/// fora do escopo mínimo de E7.2, que só exige idempotência + DLQ).
/// </summary>
public sealed partial class SyncRequestedConsumerService : BackgroundService
{
    private readonly RabbitMqConnectionOptions _options;
    private readonly IConnectorSyncProcessor _processor;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly ILogger<SyncRequestedConsumerService> _logger;

    private IConnection? _connection;
    private IChannel? _channel;

    public SyncRequestedConsumerService(
        RabbitMqConnectionOptions options,
        IConnectorSyncProcessor processor,
        ITenantContextAccessor tenantContextAccessor,
        ILogger<SyncRequestedConsumerService> logger)
    {
        _options = options;
        _processor = processor;
        _tenantContextAccessor = tenantContextAccessor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _connection = await new ConnectionFactory { Uri = new Uri(_options.ConnectionString) }.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await ConnectorMessagingTopology.DeclareAsync(_channel, stoppingToken);
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
            LogProcessingFailed(ex, message.SyncRunId);
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Falha ao processar SyncRun {SyncRunId} — enviado para a DLQ.")]
    private partial void LogProcessingFailed(Exception exception, Guid syncRunId);
}
