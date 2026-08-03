using System.Text;
using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
using EIP.Data.Pipeline;
using EIP.Data.Semantic.Application;
using EIP.Data.Warehouse.Application;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Canonical;
using EIP.Shared.Contracts.Tenancy;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Data.Warehouse.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (Testcontainers), o teste de reconciliação exigido para toda
/// métrica certificada (E6.1, docs/09-Data-Warehouse.md §9): Receita Líquida/Quantidade Faturada/
/// Ticket Médio excluem documentos cancelados e batem com os valores esperados calculados à mão.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class MetricsQueryServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly IWarehouseLoadService _warehouseLoadService;
    private readonly IMetricsQueryService _metricsQueryService;
    private readonly IDbContextFactory<TenantDbContext> _tenantDbContextFactory;

    public MetricsQueryServiceTests(SqlServerContainerFixture fixture)
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

        services.AddSingleton<ICanonicalRecordStore, CanonicalRecordStore>();
        services.AddSingleton<IPipelineProcessor, PipelineProcessor>();
        services.AddSingleton<ITenantDirectory, TenantDirectory>();
        services.AddSingleton<IWarehouseLoadStore, WarehouseLoadStore>();
        services.AddSingleton<IWarehouseLoadService, WarehouseLoadService>();
        services.AddSingleton<IMetricsQueryService, MetricsQueryService>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _warehouseLoadService = _services.GetRequiredService<IWarehouseLoadService>();
        _metricsQueryService = _services.GetRequiredService<IMetricsQueryService>();
        _tenantDbContextFactory = _services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task GetCommercialMetricsAsync_ExcludesCanceledInvoices_AndComputesCorrectAggregates()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "MET-C1", "Cliente Métricas");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "MET-P1", "Produto Métricas");

            // Duas faturas válidas (quantidade 2 e 3, unitPrice 100 => NetAmount 200 e 300) e uma
            // cancelada com valor bem maior — precisa ser excluída das 3 métricas.
            var content = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-MET-1","issueDate":"2026-01-01","customerCode":"MET-C1","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"MET-P1","quantity":2,"unitPrice":100}]},
                 {"invoiceNumber":"NF-MET-2","issueDate":"2026-01-02","customerCode":"MET-C1","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"MET-P1","quantity":3,"unitPrice":100}]},
                 {"invoiceNumber":"NF-MET-3","issueDate":"2026-01-03","customerCode":"MET-C1","currencyCode":"BRL","status":"Canceled",
                  "items":[{"lineNumber":1,"productCode":"MET-P1","quantity":100,"unitPrice":1000}]}]
                """);
            var request = new PipelineProcessingRequest(
                tenantId, companyId, sourceSystemId, SyncRunId: Guid.NewGuid(),
                CanonicalSourceEntities.SalesInvoices, CorrelationId: Guid.NewGuid().ToString(),
                RawObjectUri: $"tests/{Guid.NewGuid():N}.json", RawContent: content);
            var pipelineResult = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
            pipelineResult.AcceptedCount.Should().Be(3);

            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            var metrics = await _metricsQueryService.GetCommercialMetricsAsync(new MetricsQueryFilter(tenantId, null, null, null), CancellationToken.None);

            metrics.NetRevenue.Value.Should().Be(500m);
            metrics.InvoicedQuantity.Value.Should().Be(5m);
            metrics.AverageTicket.Value.Should().Be(250m);

            metrics.NetRevenue.Definition.Should().Be(CertifiedMetrics.NetRevenue);
            metrics.InvoicedQuantity.Definition.Should().Be(CertifiedMetrics.InvoicedQuantity);
            metrics.AverageTicket.Definition.Should().Be(CertifiedMetrics.AverageTicket);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task GetCommercialMetricsAsync_WhenNoValidInvoices_ReturnsNullAverageTicket_NotZero()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "MET-C2", "Cliente Sem Faturas Válidas");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "MET-P2", "Produto Sem Faturas Válidas");

            var content = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-MET-CANCELED","issueDate":"2026-01-01","customerCode":"MET-C2","currencyCode":"BRL","status":"Canceled",
                  "items":[{"lineNumber":1,"productCode":"MET-P2","quantity":1,"unitPrice":500}]}]
                """);
            var request = new PipelineProcessingRequest(
                tenantId, companyId, sourceSystemId, SyncRunId: Guid.NewGuid(),
                CanonicalSourceEntities.SalesInvoices, CorrelationId: Guid.NewGuid().ToString(),
                RawObjectUri: $"tests/{Guid.NewGuid():N}.json", RawContent: content);
            await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            var metrics = await _metricsQueryService.GetCommercialMetricsAsync(new MetricsQueryFilter(tenantId, null, null, null), CancellationToken.None);

            metrics.NetRevenue.Value.Should().Be(0m);
            metrics.InvoicedQuantity.Value.Should().Be(0m);
            metrics.AverageTicket.Value.Should().BeNull();
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

            var tenant = EIP.Platform.Tenant.Domain.Tenant.Create($"Metrics Test Tenant {Guid.NewGuid():N}", $"metrics-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var company = EIP.Platform.Tenant.Domain.Company.Create(tenant.Id, "Empresa Teste Métricas", "BRL", "BR");
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            return (tenant.Id, company.Id);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task SeedCustomerAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Customers, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
    }

    private async Task SeedProductAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}","productType":"Product"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Products, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
    }

    private static PipelineProcessingRequest BuildRequest(Guid tenantId, Guid companyId, Guid sourceSystemId, string sourceEntity, byte[] rawContent) =>
        new(
            tenantId,
            companyId,
            sourceSystemId,
            SyncRunId: Guid.NewGuid(),
            sourceEntity,
            CorrelationId: Guid.NewGuid().ToString(),
            RawObjectUri: $"tests/{Guid.NewGuid():N}.json",
            RawContent: rawContent);
}
