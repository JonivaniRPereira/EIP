using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Tenant.Infrastructure;

public sealed class TenantDbContext : DbContext
{
    public const string Schema = "tenant";

    public DbSet<Domain.Tenant> Tenants => Set<Domain.Tenant>();

    public DbSet<Domain.Company> Companies => Set<Domain.Company>();

    public DbSet<Domain.Membership> Memberships => Set<Domain.Membership>();

    public TenantDbContext(DbContextOptions<TenantDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TenantDbContext).Assembly);
    }
}
