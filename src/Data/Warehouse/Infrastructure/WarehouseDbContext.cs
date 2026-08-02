using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;

namespace EIP.Data.Warehouse.Infrastructure;

public sealed class WarehouseDbContext : DbContext
{
    public const string Schema = "warehouse";

    public DbSet<DimTenant> DimTenants => Set<DimTenant>();

    public DbSet<DimCompany> DimCompanies => Set<DimCompany>();

    public DbSet<DimDate> DimDates => Set<DimDate>();

    public DbSet<DimCurrency> DimCurrencies => Set<DimCurrency>();

    public DbSet<DimCustomer> DimCustomers => Set<DimCustomer>();

    public DbSet<DimProduct> DimProducts => Set<DimProduct>();

    public DbSet<DimProductCategory> DimProductCategories => Set<DimProductCategory>();

    public DbSet<FactSalesInvoiceItem> FactSalesInvoiceItems => Set<FactSalesInvoiceItem>();

    public DbSet<LoadBatch> LoadBatches => Set<LoadBatch>();

    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WarehouseDbContext).Assembly);
    }
}
