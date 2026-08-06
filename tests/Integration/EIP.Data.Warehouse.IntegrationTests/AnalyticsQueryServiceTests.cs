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
/// Prova, contra um SQL Server real (Testcontainers), o agrupamento dimensional mínimo do Analytics
/// Engine (Fase 2, E1.1, `docs/roadmap/fase-2-backlog.md`): as 3 métricas certificadas calculadas por
/// grupo (`date.month`/`customer`/`product`) batem com o cálculo manual, documentos cancelados
/// continuam excluídos (mesma regra do E6.1), e cada grupo nunca mistura outro grupo.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AnalyticsQueryServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly IWarehouseLoadService _warehouseLoadService;
    private readonly IAnalyticsQueryService _analyticsQueryService;
    private readonly IDbContextFactory<TenantDbContext> _tenantDbContextFactory;

    public AnalyticsQueryServiceTests(SqlServerContainerFixture fixture)
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
        services.AddSingleton<IAnalyticsQueryService, AnalyticsQueryService>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _warehouseLoadService = _services.GetRequiredService<IWarehouseLoadService>();
        _analyticsQueryService = _services.GetRequiredService<IAnalyticsQueryService>();
        _tenantDbContextFactory = _services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task QueryCommercialByDimensionAsync_GroupedByDateMonth_ReturnsOneRowPerMonth_WithCorrectAggregates()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "AN-C1", "Cliente Analytics");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "AN-P1", "Produto Analytics");

            // Janeiro: 2 faturas válidas (quantidade 2+3, NetAmount 200+300=500) + 1 cancelada
            // (excluída). Fevereiro: 1 fatura válida (quantidade 1, NetAmount 1000).
            var content = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-AN-1","issueDate":"2026-01-01","customerCode":"AN-C1","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"AN-P1","quantity":2,"unitPrice":100}]},
                 {"invoiceNumber":"NF-AN-2","issueDate":"2026-01-15","customerCode":"AN-C1","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"AN-P1","quantity":3,"unitPrice":100}]},
                 {"invoiceNumber":"NF-AN-3","issueDate":"2026-01-20","customerCode":"AN-C1","currencyCode":"BRL","status":"Canceled",
                  "items":[{"lineNumber":1,"productCode":"AN-P1","quantity":100,"unitPrice":1000}]},
                 {"invoiceNumber":"NF-AN-4","issueDate":"2026-02-05","customerCode":"AN-C1","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"AN-P1","quantity":1,"unitPrice":1000}]}]
                """);
            var request = new PipelineProcessingRequest(
                tenantId, companyId, sourceSystemId, SyncRunId: Guid.NewGuid(),
                CanonicalSourceEntities.SalesInvoices, CorrelationId: Guid.NewGuid().ToString(),
                RawObjectUri: $"tests/{Guid.NewGuid():N}.json", RawContent: content);
            var pipelineResult = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
            pipelineResult.AcceptedCount.Should().Be(4);

            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            var rows = await _analyticsQueryService.QueryCommercialByDimensionAsync(
                new AnalyticsQueryFilter(tenantId, null, null, null, AnalyticsDimension.DateMonth), CancellationToken.None);

            rows.Should().HaveCount(2);

            var january = rows.Should().ContainSingle(r => r.DimensionKey == "2026-01").Which;
            january.DimensionLabel.Should().Be("2026-01");
            january.Metrics.NetRevenue.Value.Should().Be(500m);
            january.Metrics.InvoicedQuantity.Value.Should().Be(5m);
            january.Metrics.AverageTicket.Value.Should().Be(250m);

            var february = rows.Should().ContainSingle(r => r.DimensionKey == "2026-02").Which;
            february.Metrics.NetRevenue.Value.Should().Be(1000m);
            february.Metrics.InvoicedQuantity.Value.Should().Be(1m);
            february.Metrics.AverageTicket.Value.Should().Be(1000m);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task QueryCommercialByDimensionAsync_GroupedByCustomer_NeverMixesTwoCustomers()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "AN-C2", "Cliente A");
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "AN-C3", "Cliente B");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "AN-P2", "Produto Analytics 2");

            var content = Encoding.UTF8.GetBytes(
                """
                [{"invoiceNumber":"NF-AN-5","issueDate":"2026-01-01","customerCode":"AN-C2","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"AN-P2","quantity":1,"unitPrice":100}]},
                 {"invoiceNumber":"NF-AN-6","issueDate":"2026-01-02","customerCode":"AN-C3","currencyCode":"BRL","status":"Issued",
                  "items":[{"lineNumber":1,"productCode":"AN-P2","quantity":1,"unitPrice":900}]}]
                """);
            var request = new PipelineProcessingRequest(
                tenantId, companyId, sourceSystemId, SyncRunId: Guid.NewGuid(),
                CanonicalSourceEntities.SalesInvoices, CorrelationId: Guid.NewGuid().ToString(),
                RawObjectUri: $"tests/{Guid.NewGuid():N}.json", RawContent: content);
            await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            var rows = await _analyticsQueryService.QueryCommercialByDimensionAsync(
                new AnalyticsQueryFilter(tenantId, null, null, null, AnalyticsDimension.Customer), CancellationToken.None);

            rows.Should().HaveCount(2);
            rows.Should().ContainSingle(r => r.DimensionLabel == "Cliente A" && r.Metrics.NetRevenue.Value == 100m);
            rows.Should().ContainSingle(r => r.DimensionLabel == "Cliente B" && r.Metrics.NetRevenue.Value == 900m);
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

            var tenant = EIP.Platform.Tenant.Domain.Tenant.Create($"Analytics Test Tenant {Guid.NewGuid():N}", $"analytics-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var company = EIP.Platform.Tenant.Domain.Company.Create(tenant.Id, "Empresa Teste Analytics", "BRL", "BR");
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
