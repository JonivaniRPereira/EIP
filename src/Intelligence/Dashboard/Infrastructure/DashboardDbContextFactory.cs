using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EIP.Intelligence.Dashboard.Infrastructure;

/// <summary>Usada apenas por `dotnet ef migrations` (design-time). Em runtime, a conexão e o
/// interceptor são registrados via DI pelo Host.</summary>
public sealed class DashboardDbContextFactory : IDesignTimeDbContextFactory<DashboardDbContext>
{
    // Mesmo valor "dev only" já público em deploy/docker-compose/.env.example — nunca um segredo real.
    private const string LocalDevConnectionString =
        "Server=localhost,1433;Database=EIP;User Id=sa;Password=Dev_OnlyChangeMe_123!;TrustServerCertificate=True;";

    public DashboardDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EIP_DASHBOARD_DB_CONNECTION") ?? LocalDevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<DashboardDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", DashboardDbContext.Schema));

        return new DashboardDbContext(optionsBuilder.Options);
    }
}
