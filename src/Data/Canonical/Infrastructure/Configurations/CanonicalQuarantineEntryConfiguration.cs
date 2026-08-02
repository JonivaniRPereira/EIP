using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Canonical.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class CanonicalQuarantineEntryConfiguration : IEntityTypeConfiguration<CanonicalQuarantineEntry>
{
    public void Configure(EntityTypeBuilder<CanonicalQuarantineEntry> builder)
    {
        builder.ToTable("QuarantineEntries");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.TenantId).IsRequired();
        builder.Property(q => q.SourceEntity).HasMaxLength(200).IsRequired();
        builder.Property(q => q.RawObjectUri).HasMaxLength(2000).IsRequired();
        builder.Property(q => q.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(q => q.FailedRule).HasMaxLength(200).IsRequired();
        builder.Property(q => q.Reason).HasMaxLength(2000).IsRequired();

        builder.HasIndex(q => q.TenantId);
        builder.HasIndex(q => q.SyncRunId);
    }
}
