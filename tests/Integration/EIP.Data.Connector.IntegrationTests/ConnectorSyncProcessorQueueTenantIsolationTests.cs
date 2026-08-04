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
/// Prova, contra um SQL Server real (Testcontainers), que a fila assíncrona (RabbitMQ → Worker →
/// <see cref="ConnectorSyncProcessor"/>) preserva isolamento de tenant — critério de saída da Fase 1
/// (docs/roadmap/fase-1-backlog.md §4: "tenant, empresa, cache, fila e Object Storage preservam
/// isolamento em testes", E8.1/E8.3). Cenário: um tenant B legítimo consegue de alguma forma (ex.:
/// enumeração de Guid) obter o <c>ConnectorInstanceId</c> de um conector do tenant A e monta/reenvia
/// uma <see cref="SyncRequestedMessage"/> reivindicando ser dono dele — mesmo padrão de ataque IDOR já
/// coberto na camada HTTP por <c>ConnectorCrossTenantIsolationTests.RequestSync_...EvenWithAdulteratedIdInRoute</c>,
/// aqui reproduzido na camada de fila/worker, onde não há claim JWT — só o <c>TenantId</c> da própria
/// mensagem (<see cref="SyncRequestedConsumerService"/>, docs/05 §12).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConnectorSyncProcessorQueueTenantIsolationTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IConnectorSyncStore _store;
    private readonly RecordingReferenceRestClient _restClient;
    private readonly ConnectorSyncProcessor _processor;

    public ConnectorSyncProcessorQueueTenantIsolationTests(SqlServerContainerFixture fixture)
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
        _restClient = new RecordingReferenceRestClient();

        _processor = new ConnectorSyncProcessor(
            _store,
            _restClient,
            new NoOpRawObjectStore(),
            new FixedPipelineProcessor(),
            new UnusedCanonicalReconciliationService(),
            new UnusedWarehouseLoadService(),
            new UnusedWarehouseReconciliationService(),
            NullLogger<ConnectorSyncProcessor>.Instance);
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task ProcessAsync_MessageClaimingAnotherTenantsConnectorInstance_FailsTheRun_NeverExtractsOrAdvancesWatermark()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantA);
        ConnectorInstance instanceOwnedByTenantA;
        try
        {
            instanceOwnedByTenantA = ConnectorInstance.Create(tenantA, Guid.NewGuid(), "Conector do Tenant A", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instanceOwnedByTenantA, CancellationToken.None);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }

        // O SyncRun "legítimo" pertence ao Tenant B — é a própria mensagem da fila (com o
        // ConnectorInstanceId de A) que tenta se passar por uma sincronização de A, exatamente como o
        // worker recebe da fila (sem claim JWT, só o TenantId embutido na mensagem).
        _tenantContextAccessor.Current = new TenantContext(tenantB);
        SyncRun runOwnedByTenantB;
        try
        {
            runOwnedByTenantB = SyncRun.CreatePending(tenantB, instanceOwnedByTenantA.Id, Guid.NewGuid().ToString());
            await _store.SaveNewRunAsync(runOwnedByTenantB, CancellationToken.None);

            var message = new SyncRequestedMessage(
                runOwnedByTenantB.Id,
                instanceOwnedByTenantA.Id,
                tenantB,
                runOwnedByTenantB.CorrelationId,
                SyncRequestedMessage.CurrentContractVersion);

            await _processor.ProcessAsync(message, CancellationToken.None);

            var reloadedRun = await _store.FindRunAsync(runOwnedByTenantB.Id, CancellationToken.None);
            reloadedRun!.Status.Should().Be(SyncRunStatus.Failed);
            reloadedRun.ErrorMessage.Should().NotBeNullOrEmpty();

            // Nunca chegou a extrair da origem — a checagem de tenant acontece antes de qualquer
            // chamada à origem/gravação no Data Lake.
            _restClient.ReceivedUpdatedSince.Should().BeEmpty();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }

        // O watermark da instância real (Tenant A) nunca foi tocado pela tentativa do Tenant B.
        _tenantContextAccessor.Current = new TenantContext(tenantA);
        try
        {
            var reloadedInstance = await _store.FindInstanceAsync(instanceOwnedByTenantA.Id, CancellationToken.None);
            reloadedInstance!.LastWatermark.Should().BeNull();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }
}
