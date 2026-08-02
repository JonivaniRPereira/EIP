using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Connector.Infrastructure;

public sealed class ConnectorDbContext : DbContext
{
    public const string Schema = "connector";

    public DbSet<Domain.ConnectorInstance> ConnectorInstances => Set<Domain.ConnectorInstance>();

    public DbSet<Domain.SyncRun> SyncRuns => Set<Domain.SyncRun>();

    public ConnectorDbContext(DbContextOptions<ConnectorDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ConnectorDbContext).Assembly);
    }
}
