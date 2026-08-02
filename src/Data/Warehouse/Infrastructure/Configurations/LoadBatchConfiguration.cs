using EIP.Data.Warehouse.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Warehouse.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto na
/// migration inicial, não aqui.</summary>
public sealed class LoadBatchConfiguration : IEntityTypeConfiguration<LoadBatch>
{
    public void Configure(EntityTypeBuilder<LoadBatch> builder)
    {
        builder.ToTable("LoadBatches");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.SourceSystemId).IsRequired();
        builder.Property(l => l.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(l => l.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(l => l.TenantId);
        builder.HasIndex(l => l.SourceSystemId);
    }
}
