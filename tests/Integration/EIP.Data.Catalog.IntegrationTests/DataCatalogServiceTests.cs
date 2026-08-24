using System.Globalization;
using System.Text;
using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Catalog.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.Connector.Domain;
using EIP.Data.Connector.Infrastructure;
using EIP.Data.Pipeline;
using EIP.Data.Warehouse.Application;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Canonical;
using EIP.Shared.Contracts.Tenancy;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Data.Catalog.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (Testcontainers), o Catálogo de Dados e Linhagem (Fase 3, E3) —
/// formaliza como consulta o que só existia como prova manual no fechamento da Fase 1 (E8.2: um item
/// específico rastreado do dado bruto no MinIO até o fato analítico via `mc stat` + consultas
/// diretas). Aqui o mesmo rastreamento é reproduzido de ponta a ponta via
/// <see cref="IDataLineageService"/>/<see cref="IMetricsCatalogService"/>, com dado real gerado pelo
/// próprio teste (Pipeline → Modelo Canônico → carga do Warehouse → SyncRun/ConnectorInstance), não
/// dado hardcoded.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class DataCatalogServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly IWarehouseLoadService _warehouseLoadService;
    private readonly IConnectorSyncStore _connectorSyncStore;
    private readonly IDataLineageService _dataLineageService;
    private readonly IMetricsCatalogService _metricsCatalogService;
    private readonly IDbContextFactory<WarehouseDbContext> _warehouseDbContextFactory;
    private readonly IDbContextFactory<TenantDbContext> _tenantDbContextFactory;

    public DataCatalogServiceTests(SqlServerContainerFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
        services.AddSingleton<TenantSessionContextInterceptor>();

        services.AddDbContextFactory<CanonicalDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString).AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddDbContextFactory<WarehouseDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString).AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddDbContextFactory<TenantDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString).AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));
        services.AddDbContextFactory<ConnectorDbContext>((sp, options) =>
            options.UseSqlServer(fixture.ConnectionString).AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

        services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
        services.AddSingleton<IPipelineProcessor, PipelineProcessor>();
        services.AddSingleton<ITenantDirectory, TenantDirectory>();
        services.AddSingleton<IWarehouseLoadStore, WarehouseLoadStore>();
        services.AddSingleton<IWarehouseLoadService, WarehouseLoadService>();
        services.AddSingleton<IWarehouseReconciliationService, WarehouseReconciliationService>();
        services.AddSingleton<IConnectorSyncStore, ConnectorSyncStore>();
        services.AddSingleton<IDataLineageService, DataLineageService>();
        services.AddSingleton<IMetricsCatalogService, MetricsCatalogService>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _warehouseLoadService = _services.GetRequiredService<IWarehouseLoadService>();
        _connectorSyncStore = _services.GetRequiredService<IConnectorSyncStore>();
        _dataLineageService = _services.GetRequiredService<IDataLineageService>();
        _metricsCatalogService = _services.GetRequiredService<IMetricsCatalogService>();
        _warehouseDbContextFactory = _services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        _tenantDbContextFactory = _services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task GetLineageAsync_FullChain_TracesFromFactBackToTheOriginatingSyncRun()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var connectorInstance = ConnectorInstance.Create(tenantId, companyId, "Conector de Linhagem", "https://example.invalid/sales-invoices", CanonicalSourceEntities.SalesInvoices);
            var sourceSystemId = connectorInstance.Id;
            await _connectorSyncStore.SaveNewInstanceAsync(connectorInstance, CancellationToken.None);

            var syncRun = SyncRun.CreatePending(tenantId, connectorInstance.Id, Guid.NewGuid().ToString());
            await _connectorSyncStore.SaveNewRunAsync(syncRun, CancellationToken.None);
            syncRun.TryStartProcessing();
            await _connectorSyncStore.SaveRunAsync(syncRun, CancellationToken.None);
            syncRun.Complete(recordsProcessed: 1, acceptedCount: 1, updatedCount: 0, rejectedCount: 0);
            await _connectorSyncStore.SaveRunAsync(syncRun, CancellationToken.None);

            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "LIN-C1", "Cliente Linhagem");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "LIN-P1", "Produto Linhagem");
            var invoiceRawUri = await SeedInvoiceAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "NF-LIN-1", "LIN-C1", "LIN-P1");

            var loadResult = await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "lineage-corr", CancellationToken.None);
            loadResult.FactRowsUpsertedCount.Should().Be(1);

            await using var warehouseDb = await _warehouseDbContextFactory.CreateDbContextAsync();
            var fact = await warehouseDb.FactSalesInvoiceItems.SingleAsync(f => f.TenantId == tenantId && f.SourceSystemId == sourceSystemId);

            var result = await _dataLineageService.GetLineageAsync(tenantId, fact.FactSalesInvoiceItemKey, CancellationToken.None);

            result.Outcome.Should().Be(DataLineageOutcome.Found);
            var lineage = result.Lineage!;

            lineage.RawObjectUri.Should().Be(invoiceRawUri);
            lineage.SourceSystemId.Should().Be(sourceSystemId);

            lineage.LoadBatch.Should().NotBeNull();
            lineage.LoadBatch!.Status.Should().Be("Succeeded");

            lineage.CanonicalRecord.Should().NotBeNull();
            lineage.CanonicalRecord!.RawObjectUri.Should().Be(invoiceRawUri);

            lineage.SyncRun.Should().NotBeNull("o SyncRunId embutido no RawObjectUri deve resolver de volta para o SyncRun real que originou o dado");
            lineage.SyncRun!.Id.Should().Be(syncRun.Id);
            lineage.SyncRun.Status.Should().Be("Succeeded");

            lineage.ConnectorInstance.Should().NotBeNull();
            lineage.ConnectorInstance!.Id.Should().Be(connectorInstance.Id);
            lineage.ConnectorInstance.Name.Should().Be("Conector de Linhagem");
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task GetLineageAsync_UnknownFactKey_ReturnsNotFound()
    {
        var (tenantId, _) = await SeedTenantAndCompanyAsync();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var result = await _dataLineageService.GetLineageAsync(tenantId, factSalesInvoiceItemKey: int.MaxValue, CancellationToken.None);
            result.Outcome.Should().Be(DataLineageOutcome.NotFound);
            result.Lineage.Should().BeNull();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ListMetricsAsync_ReflectsCertifiedMetricsMetadata_AndLiveReconciliationPerSource()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var connectorInstance = ConnectorInstance.Create(tenantId, companyId, "Conector de Métricas", "https://example.invalid/sales-invoices", CanonicalSourceEntities.SalesInvoices);
            var sourceSystemId = connectorInstance.Id;
            await _connectorSyncStore.SaveNewInstanceAsync(connectorInstance, CancellationToken.None);

            var syncRun = SyncRun.CreatePending(tenantId, connectorInstance.Id, Guid.NewGuid().ToString());
            await _connectorSyncStore.SaveNewRunAsync(syncRun, CancellationToken.None);

            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "MET-C1", "Cliente Métricas");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "MET-P1", "Produto Métricas");
            await SeedInvoiceAsync(tenantId, companyId, sourceSystemId, syncRun.Id, "NF-MET-1", "MET-C1", "MET-P1");

            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "metrics-corr", CancellationToken.None);

            var metrics = await _metricsCatalogService.ListMetricsAsync(tenantId, CancellationToken.None);

            metrics.Should().HaveCount(3);
            metrics.Select(m => m.Name).Should().BeEquivalentTo(["net_revenue", "invoiced_quantity", "average_ticket"]);

            var netRevenue = metrics.Single(m => m.Name == "net_revenue");
            netRevenue.Owner.Should().Be("Comercial");
            netRevenue.Version.Should().Be("1.0");
            netRevenue.FormulaText.Should().Contain("NetAmount");

            netRevenue.Reconciliations.Should().ContainSingle();
            var reconciliation = netRevenue.Reconciliations[0];
            reconciliation.ConnectorInstanceId.Should().Be(connectorInstance.Id);
            reconciliation.ConnectorInstanceName.Should().Be("Conector de Métricas");
            reconciliation.IsWithinTolerance.Should().BeTrue();
            reconciliation.CanonicalCount.Should().Be(1);
            reconciliation.FactCount.Should().Be(1);
            reconciliation.CanonicalNetAmountTotal.Should().Be(reconciliation.FactNetAmountTotal);

            // As 3 métricas compartilham a mesma reconciliação por origem (todas sobre FactSalesInvoiceItem).
            metrics.Should().OnlyContain(m => m.Reconciliations.Count == 1);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ListMetricsAsync_TenantWithNoSalesInvoiceConnector_ReturnsMetricsWithEmptyReconciliations()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            // Um conector registrado, mas de uma origem que não alimenta FactSalesInvoiceItem — nunca
            // deve aparecer como reconciliação de uma métrica de faturamento (falso positivo "0 == 0").
            var otherConnector = ConnectorInstance.Create(tenantId, companyId, "Conector de Clientes", "https://example.invalid/customers", CanonicalSourceEntities.Customers);
            await _connectorSyncStore.SaveNewInstanceAsync(otherConnector, CancellationToken.None);

            var metrics = await _metricsCatalogService.ListMetricsAsync(tenantId, CancellationToken.None);

            metrics.Should().HaveCount(3);
            metrics.Should().OnlyContain(m => m.Reconciliations.Count == 0);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task<(Guid TenantId, Guid CompanyId)> SeedTenantAndCompanyAsync()
    {
        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await _tenantDbContextFactory.CreateDbContextAsync();

            var tenant = EIP.Platform.Tenant.Domain.Tenant.Create($"Catalog Test Tenant {Guid.NewGuid():N}", $"catalog-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var company = EIP.Platform.Tenant.Domain.Company.Create(tenant.Id, "Empresa Teste Catálogo", "BRL", "BR");
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            return (tenant.Id, company.Id);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task SeedCustomerAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, Guid syncRunId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, syncRunId, CanonicalSourceEntities.Customers, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
    }

    private async Task SeedProductAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, Guid syncRunId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}","productType":"Product"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, syncRunId, CanonicalSourceEntities.Products, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
    }

    private async Task<string> SeedInvoiceAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, Guid syncRunId, string invoiceNumber, string customerCode, string productCode)
    {
        var content = Encoding.UTF8.GetBytes(
            $$"""
            [{"invoiceNumber":"{{invoiceNumber}}","issueDate":"2026-01-01","customerCode":"{{customerCode}}","currencyCode":"BRL",
              "items":[{"lineNumber":1,"productCode":"{{productCode}}","quantity":2,"unitPrice":50}]}]
            """);
        var request = BuildRequest(tenantId, companyId, sourceSystemId, syncRunId, CanonicalSourceEntities.SalesInvoices, content);
        var result = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
        result.AcceptedCount.Should().Be(1);
        return request.RawObjectUri;
    }

    /// <summary>Constrói o <c>RawObjectUri</c> no mesmo formato determinístico de
    /// <c>S3RawObjectStore.BuildKey</c> (<c>{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequence}.json</c>)
    /// — o teste não usa o Data Lake real, mas precisa do MESMO formato para provar que
    /// <see cref="DataLineageService"/> extrai o <c>SyncRunId</c> corretamente do 7º segmento.</summary>
    private static PipelineProcessingRequest BuildRequest(Guid tenantId, Guid companyId, Guid sourceSystemId, Guid syncRunId, string sourceEntity, byte[] rawContent)
    {
        var now = DateTimeOffset.UtcNow;
        var rawObjectUri = string.Create(CultureInfo.InvariantCulture, $"{tenantId:D}/{sourceSystemId:D}/{sourceEntity}/{now:yyyy}/{now:MM}/{now:dd}/{syncRunId:D}/{Guid.NewGuid():N}.json");

        return new PipelineProcessingRequest(
            tenantId,
            companyId,
            sourceSystemId,
            syncRunId,
            sourceEntity,
            CorrelationId: Guid.NewGuid().ToString(),
            RawObjectUri: rawObjectUri,
            RawContent: rawContent);
    }
}
