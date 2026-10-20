using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Connector.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Application.Contracts;
using EIP.Data.Connector.Domain;
using EIP.Data.Connector.Infrastructure;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace EIP.Data.Connector.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (Testcontainers), a interação entre a idempotência do
/// <see cref="SyncRun"/> (docs/05-Connector-Framework.md §10.1) e a escada de retry do worker (E2.1
/// da Fase 3). Antes da E2.1, uma falha de processamento ia direto para a DLQ sem reentrega
/// automática — "Failed" era, na prática, terminal. A partir da E2.1 o worker republica a mesma
/// mensagem depois de um backoff, e essa reentrega tem que ser tratada como uma NOVA tentativa, não
/// como uma duplicata a ignorar (bug real encontrado na validação ao vivo da E2.3: sem este ajuste, a
/// 2ª entrega de um run Failed retornava sem fazer nada, e o worker confirmava a entrega como sucesso
/// — a mensagem nunca chegava nem a reprocessar nem à DLQ). A proteção contra duplicata continua
/// valendo para um run já <see cref="SyncRunStatus.Succeeded"/> — reprocessar produziria efeitos
/// colaterais duplicados.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConnectorSyncProcessorRetryIdempotencyTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IConnectorSyncStore _store;

    public ConnectorSyncProcessorRetryIdempotencyTests(SqlServerContainerFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
        services.AddSingleton<TenantSessionContextInterceptor>();
        services.AddDbContextFactory<ConnectorDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddSingleton<IConnectorSyncStore, ConnectorSyncStore>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _store = _services.GetRequiredService<IConnectorSyncStore>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task ProcessAsync_RedeliveryOfFailedRun_ReprocessesInsteadOfSilentlyIgnoring()
    {
        var tenantId = Guid.NewGuid();
        var restClient = new FlakyReferenceRestClient(failFirstNCalls: 1);
        var processor = CreateProcessor(restClient);

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var instance = ConnectorInstance.Create(tenantId, Guid.NewGuid(), "Conector de Teste", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instance, CancellationToken.None);

            var run = SyncRun.CreatePending(tenantId, instance.Id, Guid.NewGuid().ToString());
            await _store.SaveNewRunAsync(run, CancellationToken.None);

            var message = new SyncRequestedMessage(run.Id, instance.Id, tenantId, run.CorrelationId, SyncRequestedMessage.CurrentContractVersion);

            // 1ª entrega: falha (simula o timeout/erro transitório original) — o run vai para Failed.
            await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(message, CancellationToken.None));

            var afterFirstAttempt = await _store.FindRunAsync(run.Id, CancellationToken.None);
            afterFirstAttempt!.Status.Should().Be(SyncRunStatus.Failed);
            afterFirstAttempt.ErrorMessage.Should().NotBeNullOrEmpty();
            afterFirstAttempt.ErrorMessage.Should().NotContain("Falha simulada de origem");

            // 2ª entrega — exatamente o que o worker republica na fila de retry (E2.1) depois do
            // backoff: mesma SyncRunId, nova tentativa. Antes do fix, TryStartProcessing via IsTerminal
            // rejeitava isso e o método retornava sem chamar o REST client de novo.
            await processor.ProcessAsync(message, CancellationToken.None);

            restClient.CallCount.Should().Be(2, "a reentrega de um run Failed precisa re-executar o trabalho, não ser ignorada em silêncio");

            var afterRetry = await _store.FindRunAsync(run.Id, CancellationToken.None);
            afterRetry!.Status.Should().Be(SyncRunStatus.Succeeded);
            afterRetry.ErrorMessage.Should().BeNull("o erro da tentativa anterior não pode sobreviver a um retry bem-sucedido");
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ProcessAsync_RedeliveryOfSucceededRun_IsIgnored_NeverReprocesses()
    {
        var tenantId = Guid.NewGuid();
        var restClient = new FlakyReferenceRestClient(failFirstNCalls: 0);
        var processor = CreateProcessor(restClient);

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var instance = ConnectorInstance.Create(tenantId, Guid.NewGuid(), "Conector de Teste", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instance, CancellationToken.None);

            var run = SyncRun.CreatePending(tenantId, instance.Id, Guid.NewGuid().ToString());
            await _store.SaveNewRunAsync(run, CancellationToken.None);

            var message = new SyncRequestedMessage(run.Id, instance.Id, tenantId, run.CorrelationId, SyncRequestedMessage.CurrentContractVersion);

            await processor.ProcessAsync(message, CancellationToken.None);
            restClient.CallCount.Should().Be(1);

            // Reentrega at-least-once de um run que JÁ teve sucesso (docs/05 §10.1) — continua
            // protegida: reprocessar duplicaria efeitos colaterais já produzidos.
            await processor.ProcessAsync(message, CancellationToken.None);
            restClient.CallCount.Should().Be(1, "um run já Succeeded nunca deve ser reprocessado por uma reentrega");
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private ConnectorSyncProcessor CreateProcessor(IReferenceRestClient restClient) => new(
        _store,
        restClient,
        new NoOpRawObjectStore(),
        new FixedPipelineProcessor(),
        new UnusedCanonicalReconciliationService(),
        new UnusedWarehouseLoadService(),
        new UnusedWarehouseReconciliationService(),
        NullLogger<ConnectorSyncProcessor>.Instance);
}

/// <summary>Falha nas primeiras <paramref name="failFirstNCalls"/> chamadas, depois sempre sucede —
/// simula uma origem instável que se recupera, exatamente o cenário que a escada de retry (E2.1)
/// existe para tratar.</summary>
public sealed class FlakyReferenceRestClient(int failFirstNCalls) : IReferenceRestClient
{
    public int CallCount { get; private set; }

    public Task<byte[]> FetchRawContentAsync(string baseUrl, DateTimeOffset? updatedSince, CancellationToken cancellationToken)
    {
        CallCount++;
        if (CallCount <= failFirstNCalls)
        {
            throw new InvalidOperationException($"Falha simulada de origem (chamada {CallCount}).");
        }

        return Task.FromResult("[]"u8.ToArray());
    }
}
