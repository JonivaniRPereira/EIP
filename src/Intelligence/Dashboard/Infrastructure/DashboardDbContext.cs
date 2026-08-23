using EIP.Intelligence.Dashboard.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.Dashboard.Infrastructure;

public sealed class DashboardDbContext : DbContext
{
    public const string Schema = "dashboard";

    // "Domain.Dashboard" (não "Dashboard" bare) porque o segmento de namespace "Dashboard" do
    // próprio módulo colide com o nome do tipo — mesmo padrão já usado em TenantDbContext/
    // TenantConfiguration para o mesmíssimo problema (o módulo Tenant tem uma entidade Tenant).
    public DbSet<Domain.Dashboard> Dashboards => Set<Domain.Dashboard>();

    public DbSet<DashboardVersion> DashboardVersions => Set<DashboardVersion>();

    public DashboardDbContext(DbContextOptions<DashboardDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DashboardDbContext).Assembly);
    }
}
