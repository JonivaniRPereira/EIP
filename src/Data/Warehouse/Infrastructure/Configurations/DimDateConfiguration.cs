using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Dado de referência compartilhado — sem RLS (docs/09-Data-Warehouse.md §5.1/§5.2).
/// <see cref="DimDate.DateKey"/> é determinístico (YYYYMMDD), não identity.</summary>
public sealed class DimDateConfiguration : IEntityTypeConfiguration<DimDate>
{
    public void Configure(EntityTypeBuilder<DimDate> builder)
    {
        builder.ToTable("DimDate");
        builder.HasKey(d => d.DateKey);
        builder.Property(d => d.DateKey).ValueGeneratedNever();

        builder.Property(d => d.Date).IsRequired();

        builder.HasIndex(d => d.Date).IsUnique();
    }
}
