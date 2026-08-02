using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007).</summary>
public sealed class DimCompanyConfiguration : IEntityTypeConfiguration<DimCompany>
{
    public void Configure(EntityTypeBuilder<DimCompany> builder)
    {
        builder.ToTable("DimCompany");
        builder.HasKey(d => d.CompanyKey);
        builder.Property(d => d.CompanyKey).ValueGeneratedOnAdd();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.CompanyId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(d => d.DefaultCurrency).HasMaxLength(3).IsRequired();

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => new { d.TenantId, d.CompanyId }).IsUnique();
    }
}
