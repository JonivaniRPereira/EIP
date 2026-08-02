using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007). SCD Tipo 2 (docs/09 §6.1): múltiplas linhas por
/// <see cref="DimCustomer.CustomerId"/> ao longo do tempo — o índice filtrado garante no máximo uma
/// versão "atual" por cliente, aplicado no próprio banco, não só na lógica de carga.</summary>
public sealed class DimCustomerConfiguration : IEntityTypeConfiguration<DimCustomer>
{
    public void Configure(EntityTypeBuilder<DimCustomer> builder)
    {
        builder.ToTable("DimCustomer");
        builder.HasKey(d => d.CustomerKey);
        builder.Property(d => d.CustomerKey).ValueGeneratedOnAdd();

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.CustomerId).IsRequired();
        builder.Property(d => d.Code).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.Email).HasMaxLength(200);
        builder.Property(d => d.City).HasMaxLength(200);
        builder.Property(d => d.StateOrRegion).HasMaxLength(100);
        builder.Property(d => d.CountryCode).HasMaxLength(2);

        builder.HasIndex(d => d.TenantId);
        builder.HasIndex(d => new { d.TenantId, d.CustomerId })
            .HasDatabaseName("IX_DimCustomer_TenantId_CustomerId_CurrentOnly")
            .IsUnique()
            .HasFilter("[IsCurrent] = 1");
    }
}
