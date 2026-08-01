using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EIP.Platform.Tenant.Infrastructure;

/// <summary>Usada apenas por `dotnet ef migrations` (design-time). Em runtime, a conexão e o
/// interceptor são registrados via DI pelo Host (docs/roadmap/fase-0-backlog.md, épico E3).</summary>
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    // Mesmo valor "dev only" já público em deploy/docker-compose/.env.example — nunca um segredo real.
    private const string LocalDevConnectionString =
        "Server=localhost,1433;Database=EIP;User Id=sa;Password=Dev_OnlyChangeMe_123!;TrustServerCertificate=True;";

    public TenantDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EIP_TENANT_DB_CONNECTION") ?? LocalDevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", TenantDbContext.Schema));

        return new TenantDbContext(optionsBuilder.Options);
    }
}
