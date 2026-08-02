using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>Usada apenas por `dotnet ef migrations` (design-time). Em runtime, a conexão e o
/// interceptor são registrados via DI pelo Host/Worker.</summary>
public sealed class ConnectorDbContextFactory : IDesignTimeDbContextFactory<ConnectorDbContext>
{
    // Mesmo valor "dev only" já público em deploy/docker-compose/.env.example — nunca um segredo real.
    private const string LocalDevConnectionString =
        "Server=localhost,1433;Database=EIP;User Id=sa;Password=Dev_OnlyChangeMe_123!;TrustServerCertificate=True;";

    public ConnectorDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("EIP_CONNECTOR_DB_CONNECTION") ?? LocalDevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<ConnectorDbContext>();
        optionsBuilder.UseSqlServer(
            connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", ConnectorDbContext.Schema));

        return new ConnectorDbContext(optionsBuilder.Options);
    }
}
