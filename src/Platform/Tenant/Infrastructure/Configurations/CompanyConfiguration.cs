using EIP.Platform.Tenant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Platform.Tenant.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.TenantId).IsRequired();
        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.TaxId).HasMaxLength(50);
        builder.Property(c => c.DefaultCurrency).HasMaxLength(3).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(c => c.TenantId);
    }
}
