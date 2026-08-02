using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007). SCD Tipo 2 — mesmo raciocínio de
/// <see cref="DimCustomerConfiguration"/>.</summary>
public sealed class DimProductConfiguration : IEntityTypeConfiguration<DimProduct>
{
    public void Configure(EntityTypeBuilder<DimProduct> builder)
    {
        builder.ToTable("DimProduct");
        builder.HasKey(d => d.ProductKey);
        builder.Property(d => d.ProductKey).ValueGeneratedOnAdd();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.ProductId).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.ProductType).HasMaxLength(20).IsRequired();
        builder.Property(d => d.UnitOfMeasure).HasMaxLength(20);

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => d.CategoryKey);
        builder.HasIndex(d => new { d.TenantId, d.ProductId })
            .HasDatabaseName("IX_DimProduct_TenantId_ProductId_CurrentOnly")
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");
    }
}
