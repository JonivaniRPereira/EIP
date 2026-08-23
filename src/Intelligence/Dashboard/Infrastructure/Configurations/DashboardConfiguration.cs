using EIP.Intelligence.Dashboard.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EIP.Intelligence.Dashboard.Infrastructure.Configurations;

/// <summary>Protegida por RLS obrigatória (ADR-007) — a policy de segurança é criada via SQL bruto
/// na migration inicial, não aqui (EF Core não modela RLS nativamente). "Domain.Dashboard" (não
/// "Dashboard" bare) porque o segmento de namespace do módulo colide com o nome do tipo — mesmo
/// padrão de TenantConfiguration.</summary>
public sealed class DashboardConfiguration : IEntityTypeConfiguration<Domain.Dashboard>
{
    public void Configure(EntityTypeBuilder<Domain.Dashboard> builder)
    {
        builder.ToTable("Dashboards");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.TenantId).IsRequired();
        builder.Property(d => d.Name).HasMaxLength(200).IsRequired();
        builder.Property(d => d.OwnerId).IsRequired();
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        // Rascunho atual (editável, mutável — E3.1's PATCH) — ver WidgetListJsonConversion para o
        // porquê da conversão manual em vez de OwnsMany/ToJson.
        builder.Property(d => d.Widgets)
            .HasConversion(WidgetListJsonConversion.Converter, WidgetListJsonConversion.Comparer)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.UpdatedAt).IsRequired();

        builder.HasIndex(d => d.TenantId);
    }
}
