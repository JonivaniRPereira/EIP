using EIP.Intelligence.Dashboard.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Intelligence.Dashboard.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente).</summary>
public sealed class DashboardVersionConfiguration : IEntityTypeConfiguration<DashboardVersion>
{
    public void Configure(EntityTypeBuilder<DashboardVersion> builder)
    {
        builder.ToTable("DashboardVersions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.TenantId).IsRequired();
        builder.Property(v => v.DashboardId).IsRequired();
        builder.Property(v => v.PublishedByUserId).IsRequired();
        builder.Property(v => v.CreatedAt).IsRequired();

        // Widgets é Value Object serializado, nunca tabela própria (docs/12 §3) — ver
        // WidgetListJsonConversion para o porquê da conversão manual em vez de OwnsMany/ToJson.
        builder.Property(v => v.Widgets)
            .HasConversion(WidgetListJsonConversion.Converter, WidgetListJsonConversion.Comparer)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.HasIndex(v => v.TenantId);
        builder.HasIndex(v => v.DashboardId);
    }
}
