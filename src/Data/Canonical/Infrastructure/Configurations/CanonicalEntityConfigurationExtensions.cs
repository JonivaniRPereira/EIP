using EIP.Data.Canonical.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Data.Canonical.Infrastructure.Configurations;

/// <summary>Mapeamento comum a toda entidade canônica (docs/04-Modelo-Canonico.md §4), incluindo o
/// índice único de identidade de negócio (§4.1: <c>TenantId + SourceSystemId + SourceEntity +
/// SourceRecordId</c>) — centralizado aqui para não repetir/divergir entre as configurações
/// concretas.</summary>
internal static class CanonicalEntityConfigurationExtensions
{
    public static void ConfigureCanonicalFields<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : CanonicalEntity
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId).IsRequired();
        builder.Property(e => e.CompanyId).IsRequired();
        builder.Property(e => e.SourceSystemId).IsRequired();
        builder.Property(e => e.SourceEntity).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SourceRecordId).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SchemaVersion).HasMaxLength(20).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.RawObjectUri).HasMaxLength(2000).IsRequired();

        builder.HasIndex(e => e.TenantId);

        // Índice único de identidade de negócio (docs/04 §4.1) — garante idempotência do pipeline
        // (E3.4): reprocessar o mesmo registro de origem nunca duplica.
        builder.HasIndex(e => new { e.TenantId, e.SourceSystemId, e.SourceEntity, e.SourceRecordId })
            .IsUnique();
    }
}
