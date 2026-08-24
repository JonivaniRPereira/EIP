using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.Connector.Infrastructure;
using EIP.Testing.Infrastructure;
using EIP.Worker.Sync;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Xunit;

namespace EIP.Data.Connector.IntegrationTests;

/// <summary>
/// Prova, contra um RabbitMQ real (Testcontainers — nunca exercitado de ponta a ponta antes desta
/// fase, ver <c>HostApiFixture</c>), o comportamento de retry com backoff e DLQ do
/// <see cref="SyncRequestedConsumerService"/> (E2.1/E2.2 da Fase 3). Usa delays de retry curtos
/// (centenas de ms, não 10s/30s/90s de produção) via <see cref="ConnectorRetryOptions"/> injetável —
/// só os valores mudam, o mecanismo (fila com <c>x-message-ttl</c> + dead-letter de volta à fila
/// principal) é exatamente o de produção.
///
/// Um container por classe (<see cref="RabbitMqContainerFixture"/> como class fixture, não collection
/// fixture) — cada teste declara sua própria topologia com <see cref="ConnectorRetryOptions"/>, e
/// <see cref="ConnectorMessagingTopology.DeclareAsync"/> recusaria uma segunda declaração com
/// argumentos diferentes (PRECONDITION_FAILED) num broker compartilhado entre testes.
/// </summary>
public sealed class ConnectorRetryAndDlqTests : IClassFixture<RabbitMqContainerFixture>, IAsyncLifetime
{
    private static readonly ConnectorRetryOptions TestRetryOptions = new(
    [
        new ConnectorRetryLevel("test.connector.sync.retry.1", "test.retry.1", DelayMilliseconds: 300),
        new ConnectorRetryLevel("test.connector.sync.retry.2", "test.retry.2", DelayMilliseconds: 500),
        new ConnectorRetryLevel("test.connector.sync.retry.3", "test.retry.3", DelayMilliseconds: 700),
    ]);

    private readonly RabbitMqContainerFixture _rabbitFixture;
    private readonly ITenantContextAccessor _tenantContextAccessor = new AsyncLocalTenantContextAccessor();
    private IConnection? _publisherConnection;

    public ConnectorRetryAndDlqTests(RabbitMqContainerFixture rabbitFixture)
    {
        _rabbitFixture = rabbitFixture;
    }

    public async Task InitializeAsync()
    {
        _publisherConnection = await new ConnectionFactory { Uri = new Uri(_rabbitFixture.ConnectionString) }.CreateConnectionAsync();

        await using var channel = await _publisherConnection.CreateChannelAsync();
        await ConnectorMessagingTopology.DeclareAsync(channel, TestRetryOptions);

        // xUnit cria uma instância nova da classe por [Fact], mas o container (e portanto as filas)
        // é compartilhado entre eles (IClassFixture) — sem isto, uma mensagem reenfileirada por um
        // teste anterior (ex.: o requeue no fim do teste de DLQ) fica parada na fila principal sem
        // consumidor e é entregue ao consumer do PRÓXIMO teste junto com a mensagem dele.
        await channel.QueuePurgeAsync(ConnectorMessagingTopology.SyncRequestedQueueName);
        await channel.QueuePurgeAsync(ConnectorMessagingTopology.SyncRequestedDeadLetterQueueName);
        foreach (var level in TestRetryOptions.Levels)
        {
            await channel.QueuePurgeAsync(level.QueueName);
        }
    }

    public async Task DisposeAsync()
    {
        if (_publisherConnection is not null)
        {
            await _publisherConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task HandleMessageAsync_TransientFailure_IsReprocessedAutomaticallyAndNeverReachesDlq()
    {
        var tenantId = Guid.NewGuid();
        var message = new SyncRequestedMessage(Guid.NewGuid(), Guid.NewGuid(), tenantId, Guid.NewGuid().ToString(), SyncRequestedMessage.CurrentContractVersion);

        // Falha nas 2 primeiras tentativas (índices de retry 0 e 1 — 300ms e 500ms), sucede na 3ª.
        var processor = new ScriptedConnectorSyncProcessor(callCount => callCount >= 3);

        var consumer = new SyncRequestedConsumerService(
            new RabbitMqConnectionOptions(_rabbitFixture.ConnectionString),
            TestRetryOptions,
            processor,
            _tenantContextAccessor,
            NullLogger<SyncRequestedConsumerService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await PublishAsync(message);

            await WaitUntilAsync(() => processor.CallCount >= 3, TimeSpan.FromSeconds(20));

            // Sem chamadas extras depois do sucesso — a mensagem foi de fato reconhecida (Ack), não
            // fica presa reentregando.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            processor.CallCount.Should().Be(3);
            processor.ProcessedSyncRunIds.Should().OnlyContain(syncRunId => syncRunId == message.SyncRunId);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        await using var dlqReader = new RabbitMqConnectorDlqReader(_rabbitFixture.ConnectionString);
        var dlqMessages = await dlqReader.ListAsync(tenantId, CancellationToken.None);
        dlqMessages.Should().BeEmpty("uma falha transitória que se recupera dentro do orçamento de tentativas nunca deve chegar à DLQ");
    }

    [Fact]
    public async Task HandleMessageAsync_PersistentFailure_ExhaustsRetriesThenLandsInDlq_AndCanBeRequeued()
    {
        var tenantId = Guid.NewGuid();
        var message = new SyncRequestedMessage(Guid.NewGuid(), Guid.NewGuid(), tenantId, Guid.NewGuid().ToString(), SyncRequestedMessage.CurrentContractVersion);

        var processor = new ScriptedConnectorSyncProcessor(_ => false);

        var consumer = new SyncRequestedConsumerService(
            new RabbitMqConnectionOptions(_rabbitFixture.ConnectionString),
            TestRetryOptions,
            processor,
            _tenantContextAccessor,
            NullLogger<SyncRequestedConsumerService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await PublishAsync(message);

            // 1 tentativa original + 3 retries (índices 0/1/2) = 4 chamadas antes de esgotar.
            await WaitUntilAsync(() => processor.CallCount >= 4, TimeSpan.FromSeconds(20));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        await using var dlqReader = new RabbitMqConnectorDlqReader(_rabbitFixture.ConnectionString);
        IReadOnlyList<DlqMessage> dlqMessages = [];
        await WaitUntilAsync(
            async () =>
            {
                dlqMessages = await dlqReader.ListAsync(tenantId, CancellationToken.None);
                return dlqMessages.Count == 1;
            },
            TimeSpan.FromSeconds(20));

        dlqMessages.Should().ContainSingle(m => m.SyncRunId == message.SyncRunId);

        var outcome = await dlqReader.RequeueAsync(tenantId, message.SyncRunId, CancellationToken.None);
        outcome.Should().Be(DlqRequeueOutcome.Requeued);

        var afterRequeue = await dlqReader.ListAsync(tenantId, CancellationToken.None);
        afterRequeue.Should().BeEmpty("a mensagem reenfileirada saiu da DLQ e voltou para a fila principal");
    }

    [Fact]
    public async Task RequeueAsync_SyncRunIdBelongingToAnotherTenant_ReturnsNotFound()
    {
        var ownerTenantId = Guid.NewGuid();
        var attackerTenantId = Guid.NewGuid();
        var message = new SyncRequestedMessage(Guid.NewGuid(), Guid.NewGuid(), ownerTenantId, Guid.NewGuid().ToString(), SyncRequestedMessage.CurrentContractVersion);

        var processor = new ScriptedConnectorSyncProcessor(_ => false);

        var consumer = new SyncRequestedConsumerService(
            new RabbitMqConnectionOptions(_rabbitFixture.ConnectionString),
            TestRetryOptions,
            processor,
            _tenantContextAccessor,
            NullLogger<SyncRequestedConsumerService>.Instance);

        await consumer.StartAsync(CancellationToken.None);
        try
        {
            await PublishAsync(message);
            await WaitUntilAsync(() => processor.CallCount >= 4, TimeSpan.FromSeconds(20));
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }

        await using var dlqReader = new RabbitMqConnectorDlqReader(_rabbitFixture.ConnectionString);

        // Confia que a mensagem chegou à DLQ (mesmo cenário do teste anterior) antes de tentar o
        // ataque — evita falso-positivo por corrida.
        await WaitUntilAsync(async () => (await dlqReader.ListAsync(ownerTenantId, CancellationToken.None)).Count == 1, TimeSpan.FromSeconds(20));

        var outcome = await dlqReader.RequeueAsync(attackerTenantId, message.SyncRunId, CancellationToken.None);
        outcome.Should().Be(DlqRequeueOutcome.NotFound, "um SyncRunId de outro tenant nunca deve ser reenfileirável — mesmo padrão IDOR-safe do resto do módulo");

        var stillInDlq = await dlqReader.ListAsync(ownerTenantId, CancellationToken.None);
        stillInDlq.Should().ContainSingle(m => m.SyncRunId == message.SyncRunId, "a tentativa do tenant errado não pode ter removido a mensagem da DLQ do dono");
    }

    private async Task PublishAsync(SyncRequestedMessage message)
    {
        await using var channel = await _publisherConnection!.CreateChannelAsync();
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
            body: body);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout) =>
        await WaitUntilAsync(() => Task.FromResult(condition()), timeout);

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!await condition())
        {
            if (cts.IsCancellationRequested)
            {
                throw new TimeoutException($"Condição não satisfeita dentro de {timeout}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken.None);
        }
    }
}

/// <summary>Fake de <see cref="IConnectorSyncProcessor"/> cujo sucesso/falha é decidido por
/// <paramref name="shouldSucceed"/> — recebe o número de tentativas já feitas (1-based, incluindo a
/// atual) e devolve se esta deve suceder.</summary>
public sealed class ScriptedConnectorSyncProcessor(Func<int, bool> shouldSucceed) : IConnectorSyncProcessor
{
    public int CallCount { get; private set; }

    public List<Guid> ProcessedSyncRunIds { get; } = [];

    public Task ProcessAsync(SyncRequestedMessage message, CancellationToken cancellationToken)
    {
        CallCount++;
        ProcessedSyncRunIds.Add(message.SyncRunId);

        if (!shouldSucceed(CallCount))
        {
            throw new InvalidOperationException($"Falha simulada (tentativa {CallCount}).");
        }

        return Task.CompletedTask;
    }
}
