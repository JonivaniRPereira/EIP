using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Warehouse.Domain;
using EIP.Data.Warehouse.Infrastructure;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Warehouse.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (via EF Core, exatamente como em produção — não SQL bruto), que
/// a RLS obrigatória (ADR-007, docs/09 §4.1: "modo Shared exige RLS obrigatória, sem exceção") isola
/// entre tenants o fato analítico <see cref="FactSalesInvoiceItem"/> — mesmo padrão de
/// <c>TenantIsolationTests</c> (Fase 0, módulo Tenant) e <c>CanonicalCrossTenantIsolationTests</c>
/// (E8.1). Defesa em profundidade além do gate estrutural genérico (<c>RlsCoverageTests</c>).
///
/// Chaves substitutas (<c>TenantKey</c>/<c>CompanyKey</c>/<c>DateKey</c>/<c>CustomerKey</c>/
/// <c>ProductKey</c>/<c>CurrencyKey</c>) são valores fixos arbitrários: não há FK/navegação EF Core
/// entre o fato e as dimensões (mesma decisão deliberada do Canônico, docs/09 §5.1), então não é
/// preciso semear linhas de dimensão reais só para provar isolamento por <c>TenantId</c>.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class WarehouseCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly AsyncLocalTenantContextAccessor _tenantContextAccessor = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public WarehouseCrossTenantIsolationTests(SqlServerContainerFixture sqlServerFixture)
    {
        _connectionString = sqlServerFixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await SeedFactAsync(_tenantA);
        await SeedFactAsync(_tenantB);
    }

    public async Task DisposeAsync()
    {
        foreach (var tenantId in new[] { _tenantA, _tenantB })
        {
            _tenantContextAccessor.Current = new TenantContext(tenantId);
            await using var db = CreateDbContext();
            await db.FactSalesInvoiceItems.Where(f => f.TenantId == tenantId).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = null;
    }

    [Fact]
    public async Task Query_WithoutTenantContext_ReturnsNoRows()
    {
        _tenantContextAccessor.Current = null;
        await using var db = CreateDbContext();

        (await db.FactSalesInvoiceItems.ToListAsync()).Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
    }

    [Fact]
    public async Task Query_WithTenantAContext_ReturnsOnlyTenantARows()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        (await db.FactSalesInvoiceItems.ToListAsync()).Should().ContainSingle().Which.TenantId.Should().Be(_tenantA);
    }

    [Fact]
    public async Task Query_WithTenantAContext_NeverReturnsTenantBRows_EvenWithExplicitFilter()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        // Mesmo pedindo explicitamente os dados do Tenant B, a RLS do banco bloqueia — o isolamento
        // não depende de o código da aplicação "lembrar" de filtrar corretamente.
        (await db.FactSalesInvoiceItems.Where(f => f.TenantId == _tenantB).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Insert_FactWithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        db.FactSalesInvoiceItems.Add(BuildFact(_tenantB));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    private async Task SeedFactAsync(Guid tenantId)
    {
        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = CreateDbContext();
            db.FactSalesInvoiceItems.Add(BuildFact(tenantId));
            await db.SaveChangesAsync();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private static FactSalesInvoiceItem BuildFact(Guid tenantId) => FactSalesInvoiceItem.Create(
        tenantId,
        tenantKey: 1,
        companyKey: 1,
        dateKey: 20260101,
        customerKey: 1,
        productKey: 1,
        currencyKey: 1,
        sourceSystemId: Guid.NewGuid(),
        sourceEntity: "sales-invoices",
        sourceRecordId: $"NF-{Guid.NewGuid():N}-1",
        salesInvoiceId: Guid.NewGuid(),
        salesInvoiceItemId: Guid.NewGuid(),
        rawObjectUri: $"tests/{Guid.NewGuid():N}.json",
        loadBatchId: Guid.NewGuid(),
        invoiceNumber: $"NF-{Guid.NewGuid():N}",
        status: "Issued",
        lineNumber: 1,
        quantity: 1m,
        grossAmount: 100m,
        discountAmount: 0m,
        taxAmount: null,
        netAmount: 100m);

    private WarehouseDbContext CreateDbContext()
    {
        var interceptor = new TenantSessionContextInterceptor(_tenantContextAccessor);
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new WarehouseDbContext(options);
    }
}
