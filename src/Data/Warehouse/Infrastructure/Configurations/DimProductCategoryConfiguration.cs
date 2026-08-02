using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007). Nunca populada nesta fase — ver
/// <see cref="DimProductCategory"/>.</summary>
public sealed class DimProductCategoryConfiguration : IEntityTypeConfiguration<DimProductCategory>
{
    public void Configure(EntityTypeBuilder<DimProductCategory> builder)
    {
        builder.ToTable("DimProductCategory");
        builder.HasKey(d => d.ProductCategoryKey);
        builder.Property(d => d.ProductCategoryKey).ValueGeneratedOnAdd();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.CategoryId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => new { d.TenantId, d.CategoryId }).IsUnique();
    }
}
