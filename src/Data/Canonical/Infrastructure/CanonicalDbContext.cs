using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Canonical.Infrastructure;

public sealed class CanonicalDbContext : DbContext
{
    public const string Schema = "canonical";

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();

    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();

    public DbSet<CanonicalQuarantineEntry> QuarantineEntries => Set<CanonicalQuarantineEntry>();

    public CanonicalDbContext(DbContextOptions<CanonicalDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CanonicalDbContext).Assembly);
    }
}
