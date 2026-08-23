using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.Analytics.Application;
using EIP.Intelligence.Dashboard.Domain;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.Dashboard.Infrastructure.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (via EF Core, exatamente como em produção — não SQL bruto), que a
/// RLS obrigatória (ADR-007) isola entre tenants as duas tabelas do schema `dashboard` — mesmo padrão
/// de <c>CanonicalCrossTenantIsolationTests</c>/<c>WarehouseCrossTenantIsolationTests</c> do
/// fechamento da Fase 1. Defesa em profundidade além do gate estrutural genérico
/// (<c>RlsCoverageTests</c>, já provou em E2.1 que a policy existe e está ativa) e além do teste de
/// isolamento em nível de API (<c>DashboardCrossTenantIsolationTests</c> no
/// `EIP.Host.IntegrationTests`, E3.1, que prova 404 na API — este aqui prova que o próprio banco
/// nunca devolveria a linha, mesmo que um bug futuro no controller esquecesse de checar algo).
/// Fechamento do E5.1, docs/roadmap/fase-2-backlog.md.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class DashboardTableCrossTenantIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly AsyncLocalTenantContextAccessor _tenantContextAccessor = new();
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();

    public DashboardTableCrossTenantIsolationTests(SqlServerContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        await SeedDashboardWithPublishedVersionAsync(_tenantA, "Painel do Tenant A");
        await SeedDashboardWithPublishedVersionAsync(_tenantB, "Painel do Tenant B");
    }

    public async Task DisposeAsync()
    {
        foreach (var tenantId in new[] { _tenantA, _tenantB })
        {
            _tenantContextAccessor.Current = new TenantContext(tenantId);
            await using var db = CreateDbContext();
            await db.DashboardVersions.Where(v => v.TenantId == tenantId).ExecuteDeleteAsync();
            await db.Dashboards.Where(d => d.TenantId == tenantId).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = null;
    }

    [Fact]
    public async Task Query_WithoutTenantContext_ReturnsNoRows()
    {
        _tenantContextAccessor.Current = null;
        await using var db = CreateDbContext();

        (await db.Dashboards.ToListAsync()).Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
        (await db.DashboardVersions.ToListAsync()).Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
    }

    [Fact]
    public async Task Query_WithTenantAContext_ReturnsOnlyTenantARows()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        (await db.Dashboards.ToListAsync()).Should().ContainSingle().Which.TenantId.Should().Be(_tenantA);
        (await db.DashboardVersions.ToListAsync()).Should().ContainSingle().Which.TenantId.Should().Be(_tenantA);
    }

    [Fact]
    public async Task Query_WithTenantAContext_NeverReturnsTenantBRows_EvenWithExplicitFilter()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        // Mesmo pedindo explicitamente os dados do Tenant B, a RLS do banco bloqueia — o isolamento
        // não depende de o código da aplicação "lembrar" de filtrar corretamente.
        (await db.Dashboards.Where(d => d.TenantId == _tenantB).ToListAsync()).Should().BeEmpty();
        (await db.DashboardVersions.Where(v => v.TenantId == _tenantB).ToListAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Insert_DashboardWithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        db.Dashboards.Add(Domain.Dashboard.Create(_tenantB, "Painel Invasor", Guid.NewGuid()));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    [Fact]
    public async Task Insert_DashboardVersionWithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA);
        await using var db = CreateDbContext();

        db.DashboardVersions.Add(DashboardVersion.Create(_tenantB, Guid.NewGuid(), [BuildWidget()], Guid.NewGuid()));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    private async Task SeedDashboardWithPublishedVersionAsync(Guid tenantId, string name)
    {
        _tenantContextAccessor.Current = new TenantContext(tenantId);
        try
        {
            await using var db = CreateDbContext();

            var dashboard = Domain.Dashboard.Create(tenantId, name, Guid.NewGuid());
            dashboard.UpdateDraft(null, [BuildWidget()]);
            var version = dashboard.Publish(Guid.NewGuid());

            db.Dashboards.Add(dashboard);
            db.DashboardVersions.Add(version);
            await db.SaveChangesAsync();
        }
        finally
        {
            _tenantContextAccessor.Current = null;
        }
    }

    private static Widget BuildWidget() => new(
        WidgetType.Kpi,
        "Receita Líquida",
        new AnalyticsQueryDefinition("sales", ["netRevenue"], ["date.month"], [], [], null),
        new WidgetVisualization());

    private DashboardDbContext CreateDbContext()
    {
        var interceptor = new TenantSessionContextInterceptor(_tenantContextAccessor);
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new DashboardDbContext(options);
    }
}
