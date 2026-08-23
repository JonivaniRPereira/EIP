using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.Analytics.Application;
using EIP.Intelligence.Dashboard.Domain;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.Dashboard.Infrastructure.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E2.2 (`docs/roadmap/fase-2-backlog.md`): publicar duas vezes gera
/// duas <see cref="DashboardVersion"/> distintas, e a versão anterior continua íntegra e recuperável
/// (histórico) — nunca sobrescrita. Contra um SQL Server real (via EF Core, mesmo padrão de
/// <c>CanonicalCrossTenantIsolationTests</c>), não uma simulação em memória.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class DashboardPublishTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly AsyncLocalTenantContextAccessor _tenantContextAccessor = new();
    private readonly Guid _tenantId = Guid.NewGuid();
    private Guid _dashboardId;

    public DashboardPublishTests(SqlServerContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantId);
        await using (var db = CreateDbContext())
        {
            await db.DashboardVersions.Where(v => v.TenantId == _tenantId).ExecuteDeleteAsync();
            await db.Dashboards.Where(d => d.TenantId == _tenantId).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = null;
    }

    [Fact]
    public async Task Publish_CalledTwice_CreatesTwoDistinctVersionsAndKeepsThePreviousOneIntact()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantId);

        var dashboard = Domain.Dashboard.Create(_tenantId, "Painel Comercial", Guid.NewGuid());
        _dashboardId = dashboard.Id;
        await using (var db = CreateDbContext())
        {
            db.Dashboards.Add(dashboard);
            await db.SaveChangesAsync();
        }

        DashboardVersion firstVersion;
        await using (var db = CreateDbContext())
        {
            var tracked = await db.Dashboards.SingleAsync(d => d.Id == _dashboardId);
            tracked.UpdateDraft(null, [BuildWidget("netRevenue")]);
            firstVersion = tracked.Publish(Guid.NewGuid());
            db.DashboardVersions.Add(firstVersion);
            await db.SaveChangesAsync();
        }

        // 2ª publicação com conteúdo diferente — prova que é uma versão nova, nunca um update da
        // primeira (imutabilidade, docs/12 §4).
        DashboardVersion secondVersion;
        await using (var db = CreateDbContext())
        {
            var tracked = await db.Dashboards.SingleAsync(d => d.Id == _dashboardId);
            tracked.UpdateDraft(null, [BuildWidget("netRevenue"), BuildWidget("invoicedQuantity")]);
            secondVersion = tracked.Publish(Guid.NewGuid());
            db.DashboardVersions.Add(secondVersion);
            await db.SaveChangesAsync();
        }

        secondVersion.Id.Should().NotBe(firstVersion.Id);

        await using var readDb = CreateDbContext();
        var versions = await readDb.DashboardVersions.Where(v => v.DashboardId == _dashboardId).ToListAsync();

        versions.Should().HaveCount(2, "as duas publicações nunca podem colapsar em uma só linha");

        var persistedFirst = versions.Single(v => v.Id == firstVersion.Id);
        persistedFirst.Widgets.Should().ContainSingle("a primeira versão nunca é sobrescrita pela segunda publicação");

        var persistedSecond = versions.Single(v => v.Id == secondVersion.Id);
        persistedSecond.Widgets.Should().HaveCount(2);

        var persistedDashboard = await readDb.Dashboards.SingleAsync(d => d.Id == _dashboardId);
        persistedDashboard.Status.Should().Be(DashboardStatus.Published);
    }

    private static Widget BuildWidget(string metric) => new(
        WidgetType.Kpi,
        $"Widget {metric}",
        new AnalyticsQueryDefinition("sales", [metric], ["date.month"], [], [], null),
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
