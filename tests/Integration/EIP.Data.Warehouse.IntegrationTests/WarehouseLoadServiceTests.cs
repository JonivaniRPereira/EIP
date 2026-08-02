using System.Text;
using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Infrastructure;
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

namespace EIP.Data.Warehouse.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (Testcontainers), as garantias centrais da carga do Warehouse
/// (E5.3/E5.4, docs/09-Data-Warehouse.md §6.1/§7.1/§8.2): versionamento SCD Tipo 2 em
/// <c>DimCustomer</c>, upsert idempotente de <c>FactSalesInvoiceItem</c>, rastreabilidade até o
/// registro canônico e o objeto bruto, e reconciliação Canônico↔Fato.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class WarehouseLoadServiceTests : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly ITenantContextAccessor _tenantContextAccessor;
    private readonly IPipelineProcessor _pipelineProcessor;
    private readonly IWarehouseLoadService _warehouseLoadService;
    private readonly IWarehouseReconciliationService _warehouseReconciliationService;
    private readonly IDbContextFactory<WarehouseDbContext> _warehouseDbContextFactory;
    private readonly IDbContextFactory<TenantDbContext> _tenantDbContextFactory;

    public WarehouseLoadServiceTests(SqlServerContainerFixture fixture)
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
        services.AddSingleton<IWarehouseReconciliationService, WarehouseReconciliationService>();

        _services = services.BuildServiceProvider();
        _tenantContextAccessor = _services.GetRequiredService<ITenantContextAccessor>();
        _pipelineProcessor = _services.GetRequiredService<IPipelineProcessor>();
        _warehouseLoadService = _services.GetRequiredService<IWarehouseLoadService>();
        _warehouseReconciliationService = _services.GetRequiredService<IWarehouseReconciliationService>();
        _warehouseDbContextFactory = _services.GetRequiredService<IDbContextFactory<WarehouseDbContext>>();
        _tenantDbContextFactory = _services.GetRequiredService<IDbContextFactory<TenantDbContext>>();
    }

    public ValueTask DisposeAsync() => _services.DisposeAsync();

    [Fact]
    public async Task LoadSalesInvoiceItemsAsync_TraceableAndIdempotent_NeverDuplicatesFactRows()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "TRACE-C1", "Cliente Rastreável");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "TRACE-P1", "Produto Rastreável");
            var invoiceRawUri = await SeedInvoiceAsync(tenantId, companyId, sourceSystemId, "NF-TRACE-1", "TRACE-C1", "TRACE-P1");

            var firstLoad = await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);
            firstLoad.ItemsConsideredCount.Should().Be(1);
            firstLoad.FactRowsUpsertedCount.Should().Be(1);

            await using (var canonicalDb = await _services.GetRequiredService<IDbContextFactory<CanonicalDbContext>>().CreateDbContextAsync())
            {
                var canonicalItem = await canonicalDb.SalesInvoiceItems.SingleAsync(i => i.TenantId == tenantId && i.SourceSystemId == sourceSystemId);

                await using var warehouseDb = await _warehouseDbContextFactory.CreateDbContextAsync();
                var fact = await warehouseDb.FactSalesInvoiceItems.SingleAsync(f => f.TenantId == tenantId && f.SourceSystemId == sourceSystemId);

                fact.SalesInvoiceItemId.Should().Be(canonicalItem.Id);
                fact.SalesInvoiceId.Should().Be(canonicalItem.SalesInvoiceId);
                fact.SourceRecordId.Should().Be(canonicalItem.SourceRecordId);
                fact.RawObjectUri.Should().Be(invoiceRawUri);
                fact.NetAmount.Should().Be(canonicalItem.NetAmount);
            }

            // Recarrega sem nenhuma mudança de origem — nunca duplica a linha de fato.
            var secondLoad = await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-2", CancellationToken.None);
            secondLoad.FactRowsUpsertedCount.Should().Be(1);

            await using var db = await _warehouseDbContextFactory.CreateDbContextAsync();
            var factCount = await db.FactSalesInvoiceItems.CountAsync(f => f.TenantId == tenantId && f.SourceSystemId == sourceSystemId);
            factCount.Should().Be(1);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task LoadSalesInvoiceItemsAsync_WhenCustomerAttributeChanges_CreatesNewScd2Version_WithoutOverwritingTheOld()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            var customerId = await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "SCD-C1", "Nome Original");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "SCD-P1", "Produto SCD");
            await SeedInvoiceAsync(tenantId, companyId, sourceSystemId, "NF-SCD-1", "SCD-C1", "SCD-P1");

            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            // Mesma chave de negócio (SCD-C1 / mesmo CustomerId canônico), nome mudou.
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "SCD-C1", "Nome Renomeado");
            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-2", CancellationToken.None);

            await using var db = await _warehouseDbContextFactory.CreateDbContextAsync();
            var versions = await db.DimCustomers
                .Where(d => d.TenantId == tenantId && d.CustomerId == customerId)
                .OrderBy(d => d.EffectiveFrom)
                .ToListAsync();

            versions.Should().HaveCount(2);
            versions[0].Name.Should().Be("Nome Original");
            versions[0].IsCurrent.Should().BeFalse();
            versions[0].EffectiveTo.Should().NotBeNull();
            versions[1].Name.Should().Be("Nome Renomeado");
            versions[1].IsCurrent.Should().BeTrue();
            versions[1].EffectiveTo.Should().BeNull();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    [Fact]
    public async Task ReconcileSalesInvoiceItemsAsync_WhenFactRowIsMissing_DetectsTheDivergence()
    {
        var (tenantId, companyId) = await SeedTenantAndCompanyAsync();
        var sourceSystemId = Guid.NewGuid();

        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await SeedCustomerAsync(tenantId, companyId, sourceSystemId, "REC-C1", "Cliente Reconciliação DW");
            await SeedProductAsync(tenantId, companyId, sourceSystemId, "REC-P1", "Produto Reconciliação DW");
            await SeedInvoiceAsync(tenantId, companyId, sourceSystemId, "NF-DW-REC-1", "REC-C1", "REC-P1");

            await _warehouseLoadService.LoadSalesInvoiceItemsAsync(tenantId, sourceSystemId, "corr-1", CancellationToken.None);

            var beforeDeletion = await _warehouseReconciliationService.ReconcileSalesInvoiceItemsAsync(tenantId, sourceSystemId, 0.01m, CancellationToken.None);
            beforeDeletion.IsWithinTolerance.Should().BeTrue();

            await using (var db = await _warehouseDbContextFactory.CreateDbContextAsync())
            {
                var facts = await db.FactSalesInvoiceItems.Where(f => f.TenantId == tenantId && f.SourceSystemId == sourceSystemId).ToListAsync();
                db.FactSalesInvoiceItems.RemoveRange(facts);
                await db.SaveChangesAsync();
            }

            var afterDeletion = await _warehouseReconciliationService.ReconcileSalesInvoiceItemsAsync(tenantId, sourceSystemId, 0.01m, CancellationToken.None);
            afterDeletion.IsWithinTolerance.Should().BeFalse();
            afterDeletion.FactCount.Should().Be(0);
            afterDeletion.CanonicalCount.Should().Be(1);
            afterDeletion.Discrepancy.Should().NotBeNullOrEmpty();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    /// <summary>Cria um Tenant + Company reais via EF Core (mesmo padrão de
    /// <c>ConnectorCrossTenantIsolationTests</c>) — <see cref="ITenantDirectory"/> precisa encontrar
    /// linhas de verdade para resolver os atributos de <c>DimTenant</c>/<c>DimCompany</c>. Os Ids são
    /// gerados pelas próprias fábricas de domínio, não escolhidos pelo teste.</summary>
    private async Task<(Guid TenantId, Guid CompanyId)> SeedTenantAndCompanyAsync()
    {
        _tenantContextAccessor.Current = TenantContext.System;
        try
        {
            await using var db = await _tenantDbContextFactory.CreateDbContextAsync();

            var tenant = EIP.Platform.Tenant.Domain.Tenant.Create($"Warehouse Test Tenant {Guid.NewGuid():N}", $"wh-{Guid.NewGuid():N}", Guid.NewGuid(), "America/Sao_Paulo");
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();

            var company = EIP.Platform.Tenant.Domain.Company.Create(tenant.Id, "Empresa Teste Warehouse", "BRL", "BR");
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            return (tenant.Id, company.Id);
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private async Task<Guid> SeedCustomerAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Customers, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);

        await using var db = await _services.GetRequiredService<IDbContextFactory<CanonicalDbContext>>().CreateDbContextAsync();
        var customer = await db.Customers.SingleAsync(c => c.TenantId == tenantId && c.Code == code);
        return customer.Id;
    }

    private async Task SeedProductAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, string code, string name)
    {
        var content = Encoding.UTF8.GetBytes($$"""[{"code":"{{code}}","name":"{{name}}","productType":"Product"}]""");
        var request = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.Products, content);
        await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
    }

    private async Task<string> SeedInvoiceAsync(Guid tenantId, Guid companyId, Guid sourceSystemId, string invoiceNumber, string customerCode, string productCode)
    {
        var content = Encoding.UTF8.GetBytes(
            $$"""
            [{"invoiceNumber":"{{invoiceNumber}}","issueDate":"2026-01-01","customerCode":"{{customerCode}}","currencyCode":"BRL",
              "items":[{"lineNumber":1,"productCode":"{{productCode}}","quantity":2,"unitPrice":50}]}]
            """);
        var request = BuildRequest(tenantId, companyId, sourceSystemId, CanonicalSourceEntities.SalesInvoices, content);
        var result = await _pipelineProcessor.ProcessAsync(request, CancellationToken.None);
        result.AcceptedCount.Should().Be(1);
        return request.RawObjectUri;
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
