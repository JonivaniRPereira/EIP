using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Administration.Infrastructure;

public sealed class AnalyticsUsageEvent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public sealed class UsageDbContext(DbContextOptions<UsageDbContext> options) : DbContext(options)
{
    public const string Schema = "administration";

    public DbSet<AnalyticsUsageEvent> AnalyticsQueries => Set<AnalyticsUsageEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<AnalyticsUsageEvent>(entity =>
        {
            entity.ToTable("AnalyticsQueries");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.OccurredAt });
        });
    }
}
