using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui.</summary>
public sealed class DimTenantConfiguration : IEntityTypeConfiguration<DimTenant>
{
    public void Configure(EntityTypeBuilder<DimTenant> builder)
    {
        builder.ToTable("DimTenant");
        builder.HasKey(d => d.TenantKey);
        builder.Property(d => d.TenantKey).ValueGeneratedOnAdd();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();

        builder.HasIndex(d => d.TenantId).IsUnique();
    }
}
