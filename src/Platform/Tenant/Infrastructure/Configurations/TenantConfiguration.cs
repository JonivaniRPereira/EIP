using EIP.Platform.Tenant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Platform.Tenant.Infrastructure.Configurations;

/// <summary>O próprio Tenant não carrega TenantId (ele É o tenant) — não é protegido por RLS.
/// Apenas perfis de plataforma consultam esta tabela diretamente (docs/08-Multi-Tenant.md §2).</summary>
public sealed class TenantConfiguration : IEntityTypeConfiguration<Domain.Tenant>
{
    public void Configure(EntityTypeBuilder<Domain.Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
        builder.Property(t => t.DefaultTimezone).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(t => t.DataIsolationMode).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
