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
/// Prova, contra um SQL Server real (Testcontainers), a estratégia de watermark do E7.1/E7.2
/// (docs/04-Modelo-Canonico.md §11): uma sincronização automática avança o watermark persistido e a
/// próxima usa esse valor como filtro incremental; um reprocessamento manual por período ignora o
/// watermark salvo (usa a data explícita) e nunca o avança. O mapeamento canônico em si (E3) e a
/// reconciliação de sales-invoices (E4.3/E5.4) são deliberadamente fora de escopo aqui — por isso
/// todos os testes usam <c>SourceEntity = "customers"</c>, que não dispara aquele bloco em
/// <see cref="ConnectorSyncProcessor"/>.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConnectorSyncProcessorWatermarkTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IConnectorSyncStore _store;
    private readonly RecordingReferenceRestClient _restClient;
    private readonly ConnectorSyncProcessor _processor;

    public ConnectorSyncProcessorWatermarkTests(SqlServerContainerFixture fixture)
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
    public async Task ProcessAsync_FirstAutomaticSync_FetchesWithoutWatermark_ThenAdvancesIt()
    {
        var tenantId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var instance = ConnectorInstance.Create(tenantId, Guid.NewGuid(), "Conector de Teste", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instance, CancellationToken.None);

            var beforeSync = DateTimeOffset.UtcNow;
            await ProcessSyncAsync(instance.Id, tenantId, reprocessFromUtc: null);

            _restClient.ReceivedUpdatedSince.Should().ContainSingle().Which.Should().BeNull();

            var reloaded = await _store.FindInstanceAsync(instance.Id, CancellationToken.None);
            reloaded!.LastWatermark.Should().NotBeNull();
            reloaded.LastWatermark!.Value.Should().BeOnOrAfter(beforeSync);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ProcessAsync_SecondAutomaticSync_UsesThePreviouslyAdvancedWatermark()
    {
        var tenantId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var instance = ConnectorInstance.Create(tenantId, Guid.NewGuid(), "Conector de Teste", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instance, CancellationToken.None);

            await ProcessSyncAsync(instance.Id, tenantId, reprocessFromUtc: null);
            var afterFirstSync = (await _store.FindInstanceAsync(instance.Id, CancellationToken.None))!.LastWatermark;
            afterFirstSync.Should().NotBeNull();

            await ProcessSyncAsync(instance.Id, tenantId, reprocessFromUtc: null);

            _restClient.ReceivedUpdatedSince.Should().HaveCount(2);
            _restClient.ReceivedUpdatedSince[1].Should().Be(afterFirstSync);

            var afterSecondSync = (await _store.FindInstanceAsync(instance.Id, CancellationToken.None))!.LastWatermark;
            afterSecondSync.Should().BeOnOrAfter(afterFirstSync!.Value);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ProcessAsync_ManualReprocessFromPastDate_IgnoresSavedWatermark_AndNeverAdvancesIt()
    {
        var tenantId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var instance = ConnectorInstance.Create(tenantId, Guid.NewGuid(), "Conector de Teste", "https://example.invalid/customers", "customers");
            await _store.SaveNewInstanceAsync(instance, CancellationToken.None);

            // Estabelece um watermark real via sincronização automática, como em produção.
            await ProcessSyncAsync(instance.Id, tenantId, reprocessFromUtc: null);
            var establishedWatermark = (await _store.FindInstanceAsync(instance.Id, CancellationToken.None))!.LastWatermark;
            establishedWatermark.Should().NotBeNull();

            var reprocessFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            await ProcessSyncAsync(instance.Id, tenantId, reprocessFromUtc: reprocessFrom);

            _restClient.ReceivedUpdatedSince.Should().HaveCount(2);
            _restClient.ReceivedUpdatedSince[1].Should().Be(reprocessFrom);

            var afterManualReprocess = (await _store.FindInstanceAsync(instance.Id, CancellationToken.None))!.LastWatermark;
            afterManualReprocess.Should().Be(establishedWatermark);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task ProcessSyncAsync(Guid connectorInstanceId, Guid tenantId, DateTimeOffset? reprocessFromUtc)
    {
        var run = SyncRun.CreatePending(tenantId, connectorInstanceId, Guid.NewGuid().ToString());
        await _store.SaveNewRunAsync(run, CancellationToken.None);

        var message = new SyncRequestedMessage(
            run.Id,
            connectorInstanceId,
            tenantId,
            run.CorrelationId,
            SyncRequestedMessage.CurrentContractVersion,
            reprocessFromUtc);

        await _processor.ProcessAsync(message, CancellationToken.None);
    }
}
