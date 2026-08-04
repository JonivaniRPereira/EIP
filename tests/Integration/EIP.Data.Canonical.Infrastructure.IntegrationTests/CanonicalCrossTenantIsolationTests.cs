using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Data.Canonical.Domain;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Canonical.Infrastructure.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (via EF Core, exatamente como em produção — não SQL bruto), que
/// a RLS obrigatória (ADR-007) isola entre tenants as duas entidades canônicas mais usadas na fatia
/// Comercial: <see cref="Customer"/> e <see cref="SalesInvoice"/> — mesmo padrão de
/// <c>TenantIsolationTests</c> (Fase 0, módulo Tenant). Defesa em profundidade além do gate estrutural
/// genérico (<c>RlsCoverageTests</c>): fechamento do E8.1, docs/roadmap/fase-1-backlog.md.
///
/// <c>SalesInvoice.CustomerId</c> não tem FK/navegação EF Core (decisão deliberada de E2.2) — os Ids
/// usados aqui são apenas Guids válidos, sem precisar de um <see cref="Customer"/> real associado.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class CanonicalCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly AsyncLocalTenantContextAccessor _tenantContextAccessor = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public CanonicalCrossTenantIsolationTests(SqlServerContainerFixture sqlServerFixture)
    {
        _connectionString = sqlServerFixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await SeedCustomerAndInvoiceAsync(_tenantA, "Cliente da A");
        await SeedCustomerAndInvoiceAsync(_tenantB, "Cliente da B");
    }

    public async Task DisposeAsync()
    {
        foreach (var tenantId in new[] { _tenantA, _tenantB })
        {
            _tenantContextAccessor.Current = new TenantContext(tenantId);
            await using var db = CreateDbContext();
            await db.SalesInvoices.Where(i => i.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Customers.Where(c => c.TenantId == tenantId).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = null;
    }

    [Fact]
    public async Task Query_WithoutTenantContext_ReturnsNoRows()
    {
        _tenantContextAccessor.Current = null;
        await using var db = CreateDbContext();

        (await db.Customers.ToListAsync()).Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
        (await db.SalesInvoices.ToListAsync()).Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
    }

    [Fact]
    public async Task Query_WithTenantAContext_ReturnsOnlyTenantARows()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        (await db.Customers.ToListAsync()).Should().ContainSingle().Which.TenantId.Should().Be(_tenantA);
        (await db.SalesInvoices.ToListAsync()).Should().ContainSingle().Which.TenantId.Should().Be(_tenantA);
    }

    [Fact]
    public async Task Query_WithTenantAContext_NeverReturnsTenantBRows_EvenWithExplicitFilter()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        // Mesmo pedindo explicitamente os dados do Tenant B, a RLS do banco bloqueia — o isolamento
        // não depende de o código da aplicação "lembrar" de filtrar corretamente.
        (await db.Customers.Where(c => c.TenantId == _tenantB).ToListAsync()).Should().BeEmpty();
        (await db.SalesInvoices.Where(i => i.TenantId == _tenantB).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Insert_CustomerWithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        db.Customers.Add(Customer.Create(BuildLineage(_tenantB), $"INVASOR-{Guid.NewGuid():N}", "Cliente Invasor", isActive: true));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    [Fact]
    public async Task Insert_SalesInvoiceWithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        db.SalesInvoices.Add(SalesInvoice.Create(
            BuildLineage(_tenantB),
            $"NF-INVASOR-{Guid.NewGuid():N}",
            new DateOnly(2026, 1, 1),
            Guid.NewGuid(),
            SalesInvoiceStatus.Issued,
            "BRL",
            grossAmount: 100m,
            discountAmount: 0m,
            netAmount: 100m));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    private async Task SeedCustomerAndInvoiceAsync(Guid tenantId, string customerName)
    {
        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = CreateDbContext();

            var customer = Customer.Create(BuildLineage(tenantId), $"C-{Guid.NewGuid():N}", customerName, isActive: true);
            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            var invoice = SalesInvoice.Create(
                BuildLineage(tenantId),
                $"NF-{Guid.NewGuid():N}",
                new DateOnly(2026, 1, 1),
                customer.Id,
                SalesInvoiceStatus.Issued,
                "BRL",
                grossAmount: 100m,
                discountAmount: 0m,
                netAmount: 100m);
            db.SalesInvoices.Add(invoice);
            await db.SaveChangesAsync();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private static CanonicalLineage BuildLineage(Guid tenantId) => new(
        tenantId,
        Guid.NewGuid(),
        Guid.NewGuid(),
        "customers",
        Guid.NewGuid().ToString(),
        SourceUpdatedAt: null,
        SchemaVersion: "1.0",
        CorrelationId: Guid.NewGuid().ToString(),
        RawObjectUri: $"tests/{Guid.NewGuid():N}.json");

    private CanonicalDbContext CreateDbContext()
    {
        var interceptor = new TenantSessionContextInterceptor(_tenantContextAccessor);
        var options = new DbContextOptionsBuilder<CanonicalDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new CanonicalDbContext(options);
    }
}
