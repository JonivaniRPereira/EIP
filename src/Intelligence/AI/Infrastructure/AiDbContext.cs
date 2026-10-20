using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.AI.Infrastructure;

public sealed class AiSettingsRecord
{
    public Guid TenantId { get; set; }
    public long Revision { get; set; }
    public string Payload { get; set; } = string.Empty;
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AiDbContext(DbContextOptions<AiDbContext> options) : DbContext(options)
{
    public const string Schema = "ai";
    public DbSet<AiSettingsRecord> Settings => Set<AiSettingsRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<AiSettingsRecord>(entity =>
        {
            entity.ToTable("ProviderSettings");
            entity.HasKey(record => record.TenantId);
            entity.Property(record => record.Revision).IsConcurrencyToken();
            entity.Property(record => record.Payload).HasMaxLength(32768);
        });
    }
}
