using EIP.Platform.Tenant.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Platform.Tenant.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("Memberships");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.UserId).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(30).IsRequired();

        builder.HasIndex(m => m.TenantId);
        builder.HasIndex(m => new { m.TenantId, m.UserId }).IsUnique();
    }
}
