using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Dado de referência compartilhado — sem RLS.</summary>
public sealed class DimCurrencyConfiguration : IEntityTypeConfiguration<DimCurrency>
{
    public void Configure(EntityTypeBuilder<DimCurrency> builder)
    {
        builder.ToTable("DimCurrency");
        builder.HasKey(d => d.CurrencyKey);
        builder.Property(d => d.CurrencyKey).ValueGeneratedOnAdd();

        builder.Property(d => d.Code).HasMaxLength(3).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(d => d.Code).IsUnique();
    }
}
