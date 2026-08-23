using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Intelligence.Analytics.Application;
using EIP.Intelligence.Dashboard.Domain;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.Dashboard.Infrastructure.IntegrationTests;

/// <summary>
/// Prova o critério de aceite do E2.3 (`docs/roadmap/fase-2-backlog.md`): salvar um widget com uma
/// métrica inexistente é rejeitado antes de persistir — <c>DashboardVersion.Create</c> (chamado por
/// <c>Dashboard.UpdateDraft</c> só para validar, E3.1, e por <c>Dashboard.Publish</c>, E2.2) valida
/// contra o catálogo do Analytics Engine (E1.3) e lança antes de qualquer <c>SaveChangesAsync</c> ser
/// sequer tentado.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class WidgetQueryDefinitionValidationTests
{
    private readonly string _connectionString;
    private readonly Guid _tenantId = Guid.NewGuid();

    public WidgetQueryDefinitionValidationTests(SqlServerContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public async Task UpdateDraft_WidgetWithUnknownMetric_ThrowsAndNeverReachesTheDatabase()
    {
        var dashboard = Domain.Dashboard.Create(_tenantId, "Painel Inválido", Guid.NewGuid());
        var invalidWidget = new Widget(
            WidgetType.Kpi,
            "Widget Inválido",
            new AnalyticsQueryDefinition("sales", ["metricaQueNaoExiste"], ["date.month"], [], [], null),
            new WidgetVisualization());

        var act = () => dashboard.UpdateDraft(null, [invalidWidget]);

        act.Should().Throw<ArgumentException>().WithMessage("*metricaQueNaoExiste*");

        await using var db = CreateDbContext();
        (await db.DashboardVersions.CountAsync(v => v.TenantId == _tenantId))
            .Should().Be(0, "a validação deve rejeitar antes de qualquer tentativa de persistência");
    }

    [Fact]
    public async Task UpdateDraft_WidgetWithUnknownDataset_ThrowsAndNeverReachesTheDatabase()
    {
        var dashboard = Domain.Dashboard.Create(_tenantId, "Painel Inválido 2", Guid.NewGuid());
        var invalidWidget = new Widget(
            WidgetType.Table,
            "Widget Inválido",
            new AnalyticsQueryDefinition("financial", ["netRevenue"], ["date.month"], [], [], null),
            new WidgetVisualization());

        var act = () => dashboard.UpdateDraft(null, [invalidWidget]);

        act.Should().Throw<ArgumentException>().WithMessage("*financial*");

        await using var db = CreateDbContext();
        (await db.DashboardVersions.CountAsync(v => v.TenantId == _tenantId))
            .Should().Be(0, "a validação deve rejeitar antes de qualquer tentativa de persistência");
    }

    private DashboardDbContext CreateDbContext()
    {
        var tenantContextAccessor = new AsyncLocalTenantContextAccessor { Current = new TenantContext(_tenantId) };
        var interceptor = new TenantSessionContextInterceptor(tenantContextAccessor);
        var options = new DbContextOptionsBuilder<DashboardDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new DashboardDbContext(options);
    }
}
